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
        
        // ÉçÉOèâä˙âª
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
        
        // ÉçÉOÇï¬Ç∂ÇÈ
        public void Close()
        {
            if (IsInit())
            {
                m_logSrc.Listeners.Clear();
                m_logSrc.Close();
                m_logSrc = null;
            }
        }

        // ÉçÉOÇí«â¡
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
        
        // èâä˙âªçœÇ›Ç©í≤Ç◊ÇÈ
        public bool IsInit()
        {
            return (m_logSrc != null);
        }
    }
}
