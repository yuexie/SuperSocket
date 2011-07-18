﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SuperSocket.SocketEngine
{
    using System;

    public class AsyncSocketSender
    {
        #region Events & Delegates

        public delegate void EventError(Exception ex);
        public event EventError OnError;

        public delegate void EventSocketClosed();
        public event EventSocketClosed OnSocketClosed;

        #endregion Events & Delegates

        #region Fields

        private System.Net.Sockets.Socket m_Socket;
        private SocketBuffer m_SocketBuffer;
        private byte[] m_WorkBuffer;
        private int m_ToSend = 0;
        private AsyncCallback m_Callback;
        private object m_Locker = new object();

        private System.Threading.ReaderWriterLockSlim m_ReaderWriterLocker = new System.Threading.ReaderWriterLockSlim(System.Threading.LockRecursionPolicy.SupportsRecursion);
        private DateTime m_LastSendTime = DateTime.MaxValue;

        #endregion Fields

        #region Constructors

        public AsyncSocketSender(System.Net.Sockets.Socket socket)
        {
            m_Socket = socket;
            m_Callback = new AsyncCallback(this.Callback);
            m_SocketBuffer = new SocketBuffer();
        }

        #endregion Constructors

        #region Properties

        public DateTime LastSendTime
        {
            get
            {
                m_ReaderWriterLocker.EnterReadLock();
                try
                {
                    return m_LastSendTime;
                }
                finally
                {
                    m_ReaderWriterLocker.ExitReadLock();
                }
            }
            set
            {
                m_ReaderWriterLocker.EnterWriteLock();
                try
                {
                    m_LastSendTime = value;
                }
                finally
                {
                    m_ReaderWriterLocker.ExitWriteLock();
                }
            }
        }

        public Int32 BytesToSend
        {
            get
            {
                lock (m_Locker)
                {
                    return m_SocketBuffer.Length;
                }
            }
        }

        #endregion Properties

        #region Public Methods

        public void Send(byte[] buffer, int start, int length)
        {
            lock (m_Locker)
            {
                m_SocketBuffer.Write(buffer, start, length);
                Send();
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void Callback(IAsyncResult result)
        {
            try
            {
                lock (m_Locker)
                {
                    int sent = m_Socket.EndSend(result);
                    if (sent == 0)
                    {
                        if (OnSocketClosed != null)
                        {
                            OnSocketClosed();
                        }
                        return;
                    }
                    LastSendTime = DateTime.Now;
                    m_ToSend -= sent;
                    if (m_ToSend > 0)
                    {
                        m_Socket.BeginSend(m_WorkBuffer, m_WorkBuffer.Length - m_ToSend, m_ToSend,
                                          System.Net.Sockets.SocketFlags.None, m_Callback, null);
                    }
                    else
                    {
                        Send();
                    }
                }
            }
            catch (System.Net.Sockets.SocketException)
            {
                if (OnSocketClosed != null)
                {
                    OnSocketClosed();
                }
            }
            catch (Exception exp)
            {
                if (OnError != null)
                {
                    OnError(exp);
                }
            }
        }

        private void Send()
        {
            lock (m_Locker)
            {
                if (m_ToSend > 0)
                {//Still sending bytes from m_WorkBuffer. So do NOT overwrite it before being fully sent!!!
                    return;// The data will not be sent now, it will be sent by Callback...
                }
                m_WorkBuffer = m_SocketBuffer.Read(Math.Min(m_SocketBuffer.Length, 16348));
                m_ToSend = m_WorkBuffer.Length;
                if (m_ToSend > 0)
                {
                    m_Socket.BeginSend(m_WorkBuffer, 0, m_ToSend, System.Net.Sockets.SocketFlags.None, m_Callback, null);
                }
            }
        }

        #endregion Private Methods
    }
}
