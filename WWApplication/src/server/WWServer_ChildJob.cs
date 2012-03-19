using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Community.CsharpSqlite;
using Community.CsharpSqlite.SQLiteClient;

namespace WW
{
    // ServerMainJob����ServerChildJob�ɓn���f�[�^
    public class ServerChildJobState
    {
        public int socketID { get; set; }
        public Socket workerSocket { get; set; }
        public TraceLog traceLog { get; set; }
        public Sqlite3.sqlite3 mainDb { get; set; }
    };

    // �N���C�A���g���Ώ���
    public class ServerChildJob
    {
        // ���
        private enum State
        {
            STATE_INIT = 0,
            STATE_SHUTDOWN,

            STATE_COUNT
        }
        private int stateID;
        private FSM fsm = null;
        private IFSMInterface[] stateArray = null;

        // ���O
        public TraceLog traceLog = null;
        
        // �\�P�b�g��
        public int socketID { get; set; }
        public Socket socket = null;

        // Database
        public Sqlite3.sqlite3 mainDb = null;
        
        // �N���C�A���g���
        public int roomID = -1;
        public String userName;
        public String userComment;
        public String roomName;
        public uint packetNum;

        // ����M�p
        public List<ArraySegment<byte>> sendBuffer = new List<ArraySegment<byte>>();
        public List<ArraySegment<byte>> recvBuffer = new List<ArraySegment<byte>>();
        

        public ServerChildJob(ServerChildJobState st)
        {
            socketID = st.socketID;
            socket = st.workerSocket;
            traceLog = st.traceLog;
            mainDb = st.mainDb;

            stateArray = new IFSMInterface[(int)State.STATE_COUNT];
            stateArray[(int)State.STATE_INIT] = new WWClientState_Init();
            stateArray[(int)State.STATE_SHUTDOWN] = new WWClientState_Shutdown();

            stateID = (int)State.STATE_INIT;
            fsm = new FSM(stateArray[stateID]);
        }
        
        // �X�V����
        public void Update()
        {
            try
            {
                // �\�Ȃ��M
                if (socket.Available > 0)
                {
                    socket.Receive(recvBuffer, SocketFlags.None);
                }

                // ��ԏ���
                fsm.ExecuteState(this);
                
                // �\�Ȃ瑗�M
                if (sendBuffer.Count > 0)
                {
                    socket.Send(sendBuffer, SocketFlags.None);
                }
            }
            catch (Exception e)
            {
                traceLog.Append(TraceEventType.Error, e.Message);
            }

            // �ڑ����؂�Ă�����I������
            if (stateID != (uint)State.STATE_SHUTDOWN && !socket.Connected)
            {
                // �����ɏI������������
                socket.Disconnect(true);
            }
        }

        // �V���b�g�_�E����Ԃ��ǂ���
        public bool IsShutdown()
        {
            return (stateID == (uint)State.STATE_SHUTDOWN);
        }

        // ���O�o��
        public void WriteLog(TraceEventType ev, String msg)
        {
            traceLog.Append(ev, msg);
        }
    };

}
