using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class VolcengineAsrProtocol
{
    public const int DefaultProtocolVersion = 0b0001;

    public enum MessageType
    {
        ClientFullRequest = 0b0001,
        ClientAudioOnlyRequest = 0b0010,
        ServerFullResponse = 0b1001,
        ServerErrorResponse = 0b1111
    }

    public enum MessageTypeSpecificFlags
    {
        NoSequence = 0b0000,
        PositiveSequence = 0b0001,
        NegativeSequence = 0b0010,
        NegativeWithSequence = 0b0011
    }

    public enum SerializationType
    {
        NoSerialization = 0b0000,
        Json = 0b0001
    }

    public enum CompressionType
    {
        None = 0b0000,
        Gzip = 0b0001
    }

    [Serializable]
    public sealed class AsrResponse
    {
        public int Code;
        public int Event;
        public bool IsLastPackage;
        public int PayloadSequence;
        public int PayloadSize;
        public JObject PayloadMessage;
    }

    public static byte[] BuildFullClientRequest(int seq, VolcengineSettings settings, string uid)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var payload = new
        {
            user = new { uid = string.IsNullOrEmpty(uid) ? "unity_uid" : uid },
            audio = new
            {
                format = "wav",
                codec = "raw",
                rate = 16000,
                bits = 16,
                channel = 1,
                language = string.IsNullOrEmpty(settings.asrLanguage) ? "zh-CN" : settings.asrLanguage
            },
            request = new
            {
                model_name = string.IsNullOrEmpty(settings.asrModelName) ? "bigmodel" : settings.asrModelName,
                language = string.IsNullOrEmpty(settings.asrLanguage) ? "zh-CN" : settings.asrLanguage,
                enable_itn = settings.enableItn,
                enable_punc = settings.enablePunc,
                enable_ddc = settings.enableDdc,
                show_utterances = settings.showUtterances,
                enable_nonstream = false
            }
        };

        byte[] payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
        byte[] compressedPayload = GzipCompress(payloadBytes);

        using (MemoryStream ms = new MemoryStream())
        {
            ms.Write(BuildHeader(
                MessageType.ClientFullRequest,
                MessageTypeSpecificFlags.PositiveSequence,
                SerializationType.Json,
                CompressionType.Gzip), 0, 4);
            WriteInt32BigEndian(ms, seq);
            WriteUInt32BigEndian(ms, (uint)compressedPayload.Length);
            ms.Write(compressedPayload, 0, compressedPayload.Length);
            return ms.ToArray();
        }
    }

    public static byte[] BuildAudioOnlyRequest(int seq, byte[] audioSegment, bool isLast)
    {
        if (audioSegment == null)
        {
            throw new ArgumentNullException(nameof(audioSegment));
        }

        int packedSeq = isLast ? -Math.Abs(seq) : seq;
        MessageTypeSpecificFlags flags = isLast
            ? MessageTypeSpecificFlags.NegativeWithSequence
            : MessageTypeSpecificFlags.PositiveSequence;

        byte[] compressedSegment = GzipCompress(audioSegment);
        using (MemoryStream ms = new MemoryStream())
        {
            ms.Write(BuildHeader(
                MessageType.ClientAudioOnlyRequest,
                flags,
                SerializationType.Json,
                CompressionType.Gzip), 0, 4);
            WriteInt32BigEndian(ms, packedSeq);
            WriteUInt32BigEndian(ms, (uint)compressedSegment.Length);
            ms.Write(compressedSegment, 0, compressedSegment.Length);
            return ms.ToArray();
        }
    }

    public static AsrResponse ParseResponse(byte[] msg)
    {
        if (msg == null || msg.Length < 4)
        {
            return new AsrResponse { Code = -1 };
        }

        AsrResponse response = new AsrResponse();
        int headerSize = msg[0] & 0x0f;
        int messageType = msg[1] >> 4;
        int messageFlags = msg[1] & 0x0f;
        int serialization = msg[2] >> 4;
        int compression = msg[2] & 0x0f;
        int index = headerSize * 4;

        if (index > msg.Length)
        {
            response.Code = -1;
            return response;
        }

        if ((messageFlags & 0x01) != 0)
        {
            response.PayloadSequence = ReadInt32BigEndian(msg, ref index);
        }

        if ((messageFlags & 0x02) != 0)
        {
            response.IsLastPackage = true;
        }

        if ((messageFlags & 0x04) != 0)
        {
            response.Event = ReadInt32BigEndian(msg, ref index);
        }

        if (messageType == (int)MessageType.ServerFullResponse)
        {
            response.PayloadSize = (int)ReadUInt32BigEndian(msg, ref index);
        }
        else if (messageType == (int)MessageType.ServerErrorResponse)
        {
            response.Code = ReadInt32BigEndian(msg, ref index);
            response.PayloadSize = (int)ReadUInt32BigEndian(msg, ref index);
        }

        if (index >= msg.Length)
        {
            return response;
        }

        int payloadLength = msg.Length - index;
        byte[] payload = new byte[payloadLength];
        Buffer.BlockCopy(msg, index, payload, 0, payloadLength);

        if (compression == (int)CompressionType.Gzip)
        {
            try
            {
                payload = GzipDecompress(payload);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VolcengineAsrProtocol] GZip 解压失败: {ex.Message}");
                return response;
            }
        }

        if (serialization == (int)SerializationType.Json)
        {
            try
            {
                response.PayloadMessage = JObject.Parse(Encoding.UTF8.GetString(payload));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VolcengineAsrProtocol] 响应 JSON 解析失败: {ex.Message}");
            }
        }

        return response;
    }

    public static Dictionary<string, string> BuildAuthHeaders(VolcengineSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        Dictionary<string, string> headers = new Dictionary<string, string>
        {
            { "X-Api-Resource-Id", string.IsNullOrEmpty(settings.asrResourceId) ? "volc.bigasr.sauc.duration" : settings.asrResourceId },
            { "X-Api-Request-Id", Guid.NewGuid().ToString() },
            { "X-Api-App-Key", !string.IsNullOrEmpty(settings.appKey) ? settings.appKey : (settings.appId ?? string.Empty) }
        };

        // 兼容旧配置：若 accessKey 为空但已有 accessToken，则回退使用 accessToken 作为 X-Api-Access-Key。
        string resolvedAccessKey = !string.IsNullOrEmpty(settings.accessKey) ? settings.accessKey : settings.accessToken;
        if (!string.IsNullOrEmpty(resolvedAccessKey))
        {
            headers["X-Api-Access-Key"] = resolvedAccessKey;
        }

        return headers;
    }

    private static byte[] BuildHeader(
        MessageType messageType,
        MessageTypeSpecificFlags flags,
        SerializationType serializationType,
        CompressionType compressionType)
    {
        byte[] header = new byte[4];
        header[0] = (byte)((DefaultProtocolVersion << 4) | 1);
        header[1] = (byte)(((int)messageType << 4) | (int)flags);
        header[2] = (byte)(((int)serializationType << 4) | (int)compressionType);
        header[3] = 0x00;
        return header;
    }

    private static byte[] GzipCompress(byte[] data)
    {
        using (MemoryStream output = new MemoryStream())
        {
            using (GZipStream gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
            {
                gzip.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }
    }

    private static byte[] GzipDecompress(byte[] data)
    {
        using (MemoryStream input = new MemoryStream(data))
        using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
        using (MemoryStream output = new MemoryStream())
        {
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }

    private static void WriteInt32BigEndian(Stream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        stream.Write(bytes, 0, 4);
    }

    private static void WriteUInt32BigEndian(Stream stream, uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        stream.Write(bytes, 0, 4);
    }

    private static int ReadInt32BigEndian(byte[] data, ref int offset)
    {
        if (offset + 4 > data.Length)
        {
            return 0;
        }

        byte[] bytes = new byte[4];
        Buffer.BlockCopy(data, offset, bytes, 0, 4);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        offset += 4;
        return BitConverter.ToInt32(bytes, 0);
    }

    private static uint ReadUInt32BigEndian(byte[] data, ref int offset)
    {
        if (offset + 4 > data.Length)
        {
            return 0;
        }

        byte[] bytes = new byte[4];
        Buffer.BlockCopy(data, offset, bytes, 0, 4);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        offset += 4;
        return BitConverter.ToUInt32(bytes, 0);
    }
}
