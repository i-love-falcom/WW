using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;

namespace WW
{
    // ユーザ情報パケット
    public class UserInfoPacket
    {
        public String name;
        public String comment;
    }

    // ルーム情報パケット
    public class RoomListPacket
    {
        public uint numMenber;
        public uint maxMenber;
        public String name;
        public String comment;
    }

    // メッセージパケット
    public class MsgPacket
    {
        public String name;
        public String msg;
    }

    public class WWProtocolV1Helper
    {
        // WWプロトコルバージョン
        public const byte CMDVER = 0x01;    // Ver.1.0
        public const byte CMDSEP = 0x3a;    // 区切り文字':'
        
        // WWプロトコルコマンド
        public enum Cmd
        {
            CMD_OK,
            CMD_FAILED,

            CMD_HELLO,  // 接続開始
            CMD_BYE,    // 切断

            CMD_USER_INFO,          // ユーザ情報:名前:コメント

            CMD_REQ_ROOM_LIST,      // ルーム一覧リクエスト
            CMD_BEGIN_ROOM_LIST,    // ルーム送信開始
            CMD_ROOM_LIST,          // ルーム送信:人数:最大人数:名前:コメント
            CMD_END_ROOM_LIST,      // ルーム送信終了

            CMD_JOIN_ROOM,  // ルーム加入要求:ルーム名前
            CMD_BYE_ROOM,   // ルーム退室要求

            CMD_SEND_MSG,   // メッセージ送信:名前:メッセージ
        };

        // ushortをbyte[]に変換
        public static byte[] GetBytes(ushort data)
        {
            byte[] array = new byte[sizeof(ushort)];

            array[0] = (byte)((data >> 8) & 0xff);
            array[1] = (byte)(data & 0xff);

            return array;
        }

        // uintをbyte[]に変換
        public static byte[] GetBytes(uint data)
        {
            byte[] array = new byte[sizeof(uint)];
            
            array[0] = (byte)((data >> 24) & 0xff);
            array[1] = (byte)((data >> 16) & 0xff);
            array[2] = (byte)((data >>  8) & 0xff);
            array[3] = (byte)(data & 0xff);

            return array;
        }

        public static byte[] GetBytes(String str)
        {
            // 必要なものはエスケープしておく
            str = str.Replace("\\", "\\\\");
            str = str.Replace(":", "\\:");

            byte[] array = new byte[sizeof(uint) + (uint)str.Length + 1];
            byte[] len = GetBytes((uint)str.Length);
            byte[] s = Encoding.UTF8.GetBytes(str);

            Array.Copy(len, array, len.Length);
            array[4] = CMDSEP;
            Array.Copy(s, 0, array, 5, s.Length);

            return array;
        }

        // パラメータ無しパケット生成
        public static byte[] CreatePacketP0(byte cmd, uint num)
        {
            List<byte> data = new List<byte>();

            // ヘッダ
            data.Add(CMDVER);       // バージョン
            data.Add(CMDSEP);    // 区切り文字
            data.Add(cmd);          // コマンド
            data.Add(CMDSEP);    // 区切り文字

            // シーケンス番号
            data.AddRange(GetBytes(num));
            
            return data.ToArray();
        }

        // OKパケット生成
        public static byte[] CreateOkPacket(uint num)
        {
            return CreatePacketP0((byte)(Cmd.CMD_OK), num);
        }

        // FAILEDパケット生成
        public static byte[] CreateFailedPacket(uint num)
        {
            return CreatePacketP0((byte)(Cmd.CMD_FAILED), num);
        }

        // HELLOパケット生成
        public static byte[] CreateHelloPacket(uint num)
        {
            return CreatePacketP0((byte)(Cmd.CMD_HELLO), num);
        }

        // BYEパケット生成
        public static byte[] CreateByePacket(uint num)
        {
            return CreatePacketP0((byte)(Cmd.CMD_BYE), num);
        }

        // USER_INFOパケット生成
        public static byte[] CreateUserInfoPacket(uint num, String name, String comment)
        {
            byte cmd = (byte)(Cmd.CMD_USER_INFO);

            List<byte> data = new List<byte>();

            // ヘッダ
            data.Add(CMDVER);       // バージョン
            data.Add(CMDSEP);    // 区切り文字
            data.Add(cmd);          // コマンド
            data.Add(CMDSEP);    // 区切り文字

            // シーケンス番号
            data.AddRange(GetBytes(num));
            data.Add(CMDSEP);    // 区切り文字

            // ユーザ名
            data.AddRange(GetBytes(name));
            data.Add(CMDSEP);    // 区切り文字

            // コメント
            data.AddRange(GetBytes(comment));

            return data.ToArray();
        }

        // REQ_ROOM_LISTパケット生成
        public static byte[] CreateReqRoomListPacket(uint num)
        {
            return CreatePacketP0((byte)(Cmd.CMD_REQ_ROOM_LIST), num);
        }

        // BEGIN_ROOM_LISTパケット生成
        public static byte[] CreateBeginRoomListPacket(uint num)
        {
            return CreatePacketP0((byte)(Cmd.CMD_BEGIN_ROOM_LIST), num);
        }

        // BEGIN_ROOM_LISTパケット生成
        public static byte[] CreateRoomListPacket(uint num, ref RoomListPacket packet)
        {
            return CreateRoomListPacket(num, packet.name, packet.comment, packet.numMenber, packet.maxMenber);
        }

        // ROOM_LISTパケット生成
        public static byte[] CreateRoomListPacket(uint num, String name, String comment, uint numMember, uint maxMember)
        {
            byte cmd = (byte)(Cmd.CMD_ROOM_LIST);

            List<byte> data = new List<byte>();

            // ヘッダ
            data.Add(CMDVER);   // バージョン
            data.Add(CMDSEP);   // 区切り文字
            data.Add(cmd);      // コマンド
            data.Add(CMDSEP);   // 区切り文字

            // シーケンス番号
            data.AddRange(GetBytes(num));
            data.Add(CMDSEP);   // 区切り文字

            // メンバー数
            data.AddRange(GetBytes(numMember));
            data.Add(CMDSEP);   // 区切り文字

            // 最大メンバー数
            data.AddRange(GetBytes(maxMember));
            data.Add(CMDSEP);   // 区切り文字

            // ルーム名
            data.AddRange(GetBytes(name));
            data.Add(CMDSEP);   // 区切り文字

            // コメント
            data.AddRange(GetBytes(comment));

            return data.ToArray();
        }

        // END_ROOM_LISTパケット生成
        public static byte[] CreateEndRoomListPacket(uint num)
        {
            return CreatePacketP0((byte)(Cmd.CMD_END_ROOM_LIST), num);
        }

        // JOIN_ROOMパケット生成
        public static byte[] CreateJoinRoomPacket(uint num, String name)
        {
            byte cmd = (byte)(Cmd.CMD_JOIN_ROOM);

            List<byte> data = new List<byte>();

            // ヘッダ
            data.Add(CMDVER);       // バージョン
            data.Add(CMDSEP);    // 区切り文字
            data.Add(cmd);          // コマンド
            data.Add(CMDSEP);    // 区切り文字

            // シーケンス番号
            data.AddRange(GetBytes(num));
            data.Add(CMDSEP);    // 区切り文字

            // ルーム名
            data.AddRange(GetBytes(name));

            return data.ToArray();
        }

        // BYE_ROOMパケット生成
        public static byte[] CreateByeRoomPacket(uint num)
        {
            return CreatePacketP0((byte)(Cmd.CMD_BYE_ROOM), num);
        }

        // SEND_MSGパケット生成
        public static byte[] CreateSendMsgPacket(uint num, ref MsgPacket packet)
        {
            return CreateSendMsgPacket(num, packet.name, packet.msg);
        }
        
        // SEND_MSGパケット生成
        public static byte[] CreateSendMsgPacket(uint num, String name, String msg)
        {
            byte cmd = (byte)(Cmd.CMD_SEND_MSG);

            List<byte> data = new List<byte>();

            // ヘッダ
            data.Add(CMDVER);   // バージョン
            data.Add(CMDSEP);   // 区切り文字
            data.Add(cmd);      // コマンド
            data.Add(CMDSEP);   // 区切り文字

            // シーケンス番号
            data.AddRange(GetBytes(num));
            data.Add(CMDSEP);    // 区切り文字

            // ユーザ名
            data.AddRange(GetBytes(name));
            data.Add(CMDSEP);    // 区切り文字

            // メッセージ
            data.AddRange(GetBytes(msg));
            
            return data.ToArray();
        }
        
        // Ver.1のコマンドかどうか判定
        public static bool IsCmdV1(byte[] bytes)
        {
            return (bytes[0] == CMDVER);
        }

        // パケットコマンド取得
        public static Cmd GetCmd(byte[] bytes)
        {
            return (Cmd)bytes[2];
        }

        // パケット番号取得
        public static uint GetPacketNumber(byte[] bytes)
        {
            uint packetNumber;
            packetNumber = (uint)bytes[4] << 24;
            packetNumber |= (uint)bytes[5] << 16;
            packetNumber |= (uint)bytes[6] << 8;
            packetNumber |= (uint)bytes[7];
            return packetNumber;
        }

        public static bool GetUserInfoPacket(ref UserInfoPacket packet, byte[] bytes)
        {
            if (IsCmdV1(bytes) && GetCmd(bytes) == Cmd.CMD_USER_INFO)
            {
                uint nameLen;
                nameLen = (uint)bytes[9] << 24;
                nameLen |= (uint)bytes[10] << 16;
                nameLen |= (uint)bytes[11] << 8;
                nameLen |= (uint)bytes[12];

                uint commentLen;
                commentLen = (uint)bytes[9 + nameLen + 2] << 24;
                commentLen |= (uint)bytes[10 + nameLen + 2] << 16;
                commentLen |= (uint)bytes[11 + nameLen + 2] << 8;
                commentLen |= (uint)bytes[12 + nameLen + 2];

                byte[] nameBytes = new byte[nameLen];
                Array.Copy(bytes, 14, nameBytes, 0, nameLen);
                byte[] commentBytes = new byte[commentLen];
                Array.Copy(bytes, 14 + nameLen + 2, commentBytes, 0, commentLen);

                packet.name = Encoding.Unicode.GetString(nameBytes);
                packet.comment = Encoding.Unicode.GetString(commentBytes);

                return true;
            }
            return false;
        }

        public static bool GetRoomListPacket(ref RoomListPacket packet, byte[] bytes)
        {
            if (IsCmdV1(bytes) && GetCmd(bytes) == Cmd.CMD_USER_INFO)
            {
                packet.numMenber = (uint)bytes[9] << 24;
                packet.numMenber |= (uint)bytes[10] << 16;
                packet.numMenber |= (uint)bytes[11] << 8;
                packet.numMenber |= (uint)bytes[12];

                packet.maxMenber = (uint)bytes[14] << 24;
                packet.maxMenber |= (uint)bytes[15] << 16;
                packet.maxMenber |= (uint)bytes[16] << 8;
                packet.maxMenber |= (uint)bytes[17];

                uint nameLen;
                nameLen = (uint)bytes[19] << 24;
                nameLen |= (uint)bytes[20] << 16;
                nameLen |= (uint)bytes[21] << 8;
                nameLen |= (uint)bytes[22];

                uint commentLen;
                commentLen = (uint)bytes[19 + nameLen + 2] << 24;
                commentLen |= (uint)bytes[20 + nameLen + 2] << 16;
                commentLen |= (uint)bytes[21 + nameLen + 2] << 8;
                commentLen |= (uint)bytes[22 + nameLen + 2];

                byte[] nameBytes = new byte[nameLen];
                Array.Copy(bytes, 24, nameBytes, 0, nameLen);
                byte[] commentBytes = new byte[commentLen];
                Array.Copy(bytes, 24 + nameLen + 2, commentBytes, 0, commentLen);

                packet.name = Encoding.Unicode.GetString(nameBytes);
                packet.comment = Encoding.Unicode.GetString(commentBytes);

                return true;
            }
            return false;
        }

        public static String GetJoinRoomPacket(byte[] bytes)
        {
            String name = "";
            if (IsCmdV1(bytes) && GetCmd(bytes) == Cmd.CMD_JOIN_ROOM)
            {
                uint nameLen;
                nameLen = (uint)bytes[9] << 24;
                nameLen |= (uint)bytes[10] << 16;
                nameLen |= (uint)bytes[11] << 8;
                nameLen |= (uint)bytes[12];

                byte[] nameBytes = new byte[nameLen];
                Array.Copy(bytes, 14, nameBytes, 0, nameLen);

                name = Encoding.Unicode.GetString(nameBytes);
            }
            return name;
        }

        public static bool GetSendMsgPacket(ref MsgPacket packet, byte[] bytes)
        {
            if (IsCmdV1(bytes) && GetCmd(bytes) == Cmd.CMD_USER_INFO)
            {
                uint nameLen;
                nameLen = (uint)bytes[9] << 24;
                nameLen |= (uint)bytes[10] << 16;
                nameLen |= (uint)bytes[11] << 8;
                nameLen |= (uint)bytes[12];

                uint msgLen;
                msgLen = (uint)bytes[9 + nameLen + 2] << 24;
                msgLen |= (uint)bytes[10 + nameLen + 2] << 16;
                msgLen |= (uint)bytes[11 + nameLen + 2] << 8;
                msgLen |= (uint)bytes[12 + nameLen + 2];

                byte[] nameBytes = new byte[nameLen];
                Array.Copy(bytes, 14, nameBytes, 0, nameLen);
                byte[] msgBytes = new byte[msgLen];
                Array.Copy(bytes, 14 + nameLen + 2, msgBytes, 0, msgLen);

                packet.name = Encoding.Unicode.GetString(nameBytes);
                packet.msg = Encoding.Unicode.GetString(msgBytes);

                return true;
            }
            return false;
        }
    }
}
