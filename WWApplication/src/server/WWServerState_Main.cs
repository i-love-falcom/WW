using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Community.CsharpSqlite;
using Community.CsharpSqlite.SQLiteClient;

namespace WW
{
    class WWServerState_Main : IFSMInterface
    {
        // 入室処理
        public override void Entry(object context)
        {
        }

        // 実行
        public override bool Execute(object context)
        {
            ServerChildJob job = (ServerChildJob)context;

            if (job.recvBuffer.Count > 0)
            {
                foreach (ArraySegment<byte> seg in job.recvBuffer)
                {
                    byte[] bytes = seg.Array;
                    if (WWProtocolV1Helper.IsCmdV1(bytes))
                    {
                        if (WWProtocolV1Helper.GetCmd(bytes) == WWProtocolV1Helper.Cmd.CMD_USER_INFO)
                        {
                            Exec_UserInfo(ref job, bytes);
                        }
                        else if (WWProtocolV1Helper.GetCmd(bytes) == WWProtocolV1Helper.Cmd.CMD_REQ_ROOM_LIST)
                        {
                            Exec_ReqRoomList(ref job, bytes);
                        }
                        else if (WWProtocolV1Helper.GetCmd(bytes) == WWProtocolV1Helper.Cmd.CMD_JOIN_ROOM)
                        {
                            Exec_JoinRoom(ref job, bytes);
                        }
                        else if (WWProtocolV1Helper.GetCmd(bytes) == WWProtocolV1Helper.Cmd.CMD_BYE_ROOM)
                        {
                            Exec_ByeRoom(ref job, bytes);
                        }
                        else if (WWProtocolV1Helper.GetCmd(bytes) == WWProtocolV1Helper.Cmd.CMD_SEND_MSG)
                        {
                            Exec_SendMsg(ref job, bytes);
                        }
                        else if (WWProtocolV1Helper.GetCmd(bytes) == WWProtocolV1Helper.Cmd.CMD_BYE)
                        {
                            Exec_Bye(ref job, bytes);
                        }

                        job.recvBuffer.Remove(seg);
                    }
                }
            }
            return true;
        }

        // 退室処理
        public override void Exit(object context)
        {
        }

        // ユーザ情報パケット待ち受け
        private void Exec_UserInfo(ref ServerChildJob job, byte[] bytes)
        {
            UserInfoPacket packet = new UserInfoPacket();
            if (WWProtocolV1Helper.GetUserInfoPacket(ref packet, bytes))
            {
                try
                {
                    job.userName = packet.name;
                    job.userComment = packet.comment;

                    // ユーザ情報をDbに登録
                    Sqlite3.sqlite3_exec(job.mainDb, "BEGIN", 0, 0, 0);
                    int rc = Sqlite3.sqlite3_exec(
                        job.mainDb,
                        "INSERT INTO user (id, room_id, name, comment) value (" +
                        job.socketID.ToString() + ", " +
                        job.roomID.ToString() + ", " +
                        "'" + job.userName + "', " +
                        "'" + job.userComment + "')",
                        0, 0, 0);
                    if (rc != Sqlite3.SQLITE_OK)
                    {
                        Exception e = new Exception("sqlite3_exec error.");
                        throw e;
                    }
                    Sqlite3.sqlite3_exec(job.mainDb, "COMMIT", 0, 0, 0);

                    // OKパケットをクライアントへ送信
                    byte[] okPacket = WWProtocolV1Helper.CreateOkPacket(++job.packetNum);
                    job.sendBuffer.Add(new ArraySegment<byte>(okPacket));
                }
                catch (Exception e)
                {
                    job.WriteLog(TraceEventType.Error, e.Message);

                    // 途中でエラー発生ならロールバック
                    Sqlite3.sqlite3_exec(job.mainDb, "ROLLBACK", 0, 0, 0);
                }
            }
        }

        // ルーム一覧リクエスト
        private void Exec_ReqRoomList(ref ServerChildJob job, byte[] bytes)
        {
            try
            {
                List<RoomListPacket> list = new List<RoomListPacket>();

                // Dbからルーム一覧を取得
                Sqlite3.sqlite3_exec(job.mainDb, "BEGIN", 0, 0, 0);
                int rc = Sqlite3.sqlite3_exec(job.mainDb, "SELECT * FROM room;", ReqRoomListCallback, list, 0);
                if (rc != Sqlite3.SQLITE_OK)
                {
                    Exception e = new Exception("sqlite3_exec error.");
                    throw e;
                }
                Sqlite3.sqlite3_exec(job.mainDb, "COMMIT", 0, 0, 0);

                // 1つでも取得できればクライアントへ送信
                if (list.Count > 0)
                {
                    byte[] beginPacket = WWProtocolV1Helper.CreateBeginRoomListPacket(++job.packetNum);
                    job.sendBuffer.Add(new ArraySegment<byte>(beginPacket));

                    foreach (RoomListPacket var in list)
                    {
                        byte[] packet = WWProtocolV1Helper.CreateRoomListPacket(
                            ++job.packetNum,
                            var.name,
                            var.comment,
                            var.numMenber,
                            var.maxMenber);
                        job.sendBuffer.Add(new ArraySegment<byte>(packet));
                    }

                    byte[] endPacket = WWProtocolV1Helper.CreateEndRoomListPacket(++job.packetNum);
                    job.sendBuffer.Add(new ArraySegment<byte>(endPacket));
                }
                
            }
            catch (Exception e)
            {
                job.WriteLog(TraceEventType.Error, e.Message);

                // 途中でエラー発生ならロールバック
                Sqlite3.sqlite3_exec(job.mainDb, "ROLLBACK", 0, 0, 0);
            }
        }

        // ルーム加入
        private void Exec_JoinRoom(ref ServerChildJob job, byte[] bytes)
        {
            try
            {
                String roomName = WWProtocolV1Helper.GetJoinRoomPacket(bytes);
                List<int> roomIDList = new List<int>();
                
                Sqlite3.sqlite3_exec(job.mainDb, "BEGIN", 0, 0, 0);
                int rc = Sqlite3.sqlite3_exec(job.mainDb, "SELECT id FROM room WHERE name = '" + roomName + "' ;", GetIDCallback, roomIDList, 0);
                if (rc != Sqlite3.SQLITE_OK)
                {
                    Exception e = new Exception("sqlite3_exec error.");
                    throw e;
                }
                Sqlite3.sqlite3_exec(job.mainDb, "COMMIT", 0, 0, 0);

                // ルーム加入処理
                if (roomIDList.Count > 0)
                {
                    job.roomID = roomIDList[0];
                    job.roomName = roomName;

                    Sqlite3.sqlite3_exec(job.mainDb, "BEGIN", 0, 0, 0);
                    rc = Sqlite3.sqlite3_exec(
                        job.mainDb,
                        "UPDATE user SET room_id = " + job.roomID.ToString() + " WHERE name = '" + job.roomName + "' ;",
                        0, 0, 0);
                    if (rc != Sqlite3.SQLITE_OK)
                    {
                        Exception e = new Exception("sqlite3_exec error.");
                        throw e;
                    }
                    Sqlite3.sqlite3_exec(job.mainDb, "COMMIT", 0, 0, 0);

                    // OKパケットをクライアントへ送信
                    byte[] packet = WWProtocolV1Helper.CreateOkPacket(++job.packetNum);
                    job.sendBuffer.Add(new ArraySegment<byte>(packet));
                }
                else
                {
                    // 見つからなかったのでFailedパケットをクライアントへ送信
                    byte[] packet = WWProtocolV1Helper.CreateFailedPacket(++job.packetNum);
                    job.sendBuffer.Add(new ArraySegment<byte>(packet));
                }
            }
            catch (Exception e)
            {
                job.WriteLog(TraceEventType.Error, e.Message);

                // 途中でエラー発生ならロールバック
                Sqlite3.sqlite3_exec(job.mainDb, "ROLLBACK", 0, 0, 0);
            }
        }

        // ルーム退室
        private void Exec_ByeRoom(ref ServerChildJob job, byte[] bytes)
        {
            // 未加入ならすぐ戻る
            if (job.roomID < 0)
            {
                return;
            }
            
            try
            {
                Sqlite3.sqlite3_exec(job.mainDb, "BEGIN", 0, 0, 0);
                int rc = Sqlite3.sqlite3_exec(
                    job.mainDb,
                    "UPDATE user SET room_id = -1 WHERE name = '" + job.roomName + "' ;",
                    0, 0, 0);
                if (rc != Sqlite3.SQLITE_OK)
                {
                    Exception e = new Exception("sqlite3_exec error.");
                    throw e;
                }
                Sqlite3.sqlite3_exec(job.mainDb, "COMMIT", 0, 0, 0);

                job.roomID =  -1;
                job.roomName = "";

                // OKパケットをクライアントへ送信
                byte[] packet = WWProtocolV1Helper.CreateOkPacket(++job.packetNum);
                job.sendBuffer.Add(new ArraySegment<byte>(packet));
            }
            catch (Exception e)
            {
                job.WriteLog(TraceEventType.Error, e.Message);

                // 途中でエラー発生ならロールバック
                Sqlite3.sqlite3_exec(job.mainDb, "ROLLBACK", 0, 0, 0);
            }
        }
        
        // メッセージ送信
        private void Exec_SendMsg(ref ServerChildJob job, byte[] bytes)
        {
        }

        // 切断要求
        private void Exec_Bye(ref ServerChildJob job, byte[] bytes)
        {
        }


        // ルーム一覧取得コールバック
        public static int ReqRoomListCallback(object callbackArg, long argc, object p2, object p3)
        {
            
            String[] argv  = (String[])p2;
            String[] name = (String[])p3;

            RoomListPacket packet = new RoomListPacket();
            for (long n = 0; n < argc; ++n)
            {
                if (argv[n] != null)
                {
                    if (name[n].CompareTo("name") == 0)
                    {
                        packet.name = argv[n];
                    }
                    else if (name[n].CompareTo("comment") == 0)
                    {
                        packet.comment = argv[n];
                    }
                    else if (name[n].CompareTo("member_num") == 0)
                    {
                        packet.numMenber = uint.Parse(argv[n]);
                    }
                    else if (name[n].CompareTo("member_max") == 0)
                    {
                        packet.maxMenber = uint.Parse(argv[n]);
                    }
                }
            }
            
            // パケットを追加
            List<RoomListPacket> list = (List<RoomListPacket>)callbackArg;
            list.Add(packet);

            return Sqlite3.SQLITE_OK;
        }

        // ルーム一覧取得コールバック
        public static int GetIDCallback(object callbackArg, long argc, object p2, object p3)
        {
            String[] argv = (String[])p2;
            String[] name = (String[])p3;

            List<int> idList = (List<int>)callbackArg;
            for (long n = 0; n < argc; ++n)
            {
                if (argv[n] != null)
                {
                    if (name[n].CompareTo("id") == 0)
                    {
                        idList.Add(int.Parse(argv[n]));
                    }
                }
            }

            return Sqlite3.SQLITE_OK;
        }
    }
}
