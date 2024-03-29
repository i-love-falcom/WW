using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace WW
{
    public class TraceLog
    {
        protected TraceSource m_logSrc = null;
        
        
        ~TraceLog()
        {
            Close();
        }
        
        // ログ初期化
        public bool Init(String path, String name, SourceLevels level)
        {
            if (!IsInit())
            {
                try
                {
                    m_logSrc = new TraceSource(name, level);
                    
                    TextWriterTraceListener listener = new TextWriterTraceListener(path, "Log");
                    listener.TraceOutputOptions = TraceOptions.DateTime | TraceOptions.ProcessId | TraceOptions.ThreadId;
                    m_logSrc.Listeners.Add(listener);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                    return false;
                }
            }
            return true;
        }
        
        // ログを閉じる
        public void Close()
        {
            if (IsInit())
            {
                m_logSrc.Listeners.Clear();
                m_logSrc.Close();
                m_logSrc = null;
            }
        }

        // ログを追加
        public void Append(TraceEventType ev, String msg)
        {
            if (IsInit())
            {
                try
                {
                    m_logSrc.TraceEvent(ev, 0, msg);
                    m_logSrc.Flush();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            }
        }
        
        // 初期化済みか調べる
        public bool IsInit()
        {
            return (m_logSrc != null);
        }
    }
}
