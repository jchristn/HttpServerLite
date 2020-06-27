using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HttpServerLite
{
    internal static class Common
    {
        internal static void ParseIpPort(string ipPort, out string ip, out int port)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            ip = null;
            port = -1;

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                ip = ipPort.Substring(0, colonIndex);
                port = Convert.ToInt32(ipPort.Substring(colonIndex + 1));
            }
        }

        internal static byte[] ByteArrayShiftLeft(byte[] bytes)
        {
            byte[] ret = new byte[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) ret[i] = 0x00;

            for (int i = 1; i < bytes.Length; i++)
            {
                ret[(i - 1)] = bytes[i];
            }

            return ret;
        }

        internal static byte[] ByteArrayShiftRight(byte[] bytes)
        {
            byte[] ret = new byte[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) ret[i] = 0x00;

            for (int i = 0; i < (bytes.Length - 1); i++)
            {
                ret[(i + 1)] = bytes[i];
            }

            return ret;
        }

        internal static byte[] AppendBytes(byte[] orig, byte[] append)
        {
            if (orig == null && append == null) return null;

            byte[] ret = null;

            if (append == null)
            {
                ret = new byte[orig.Length];
                Buffer.BlockCopy(orig, 0, ret, 0, orig.Length);
                return ret;
            }

            if (orig == null)
            {
                ret = new byte[append.Length];
                Buffer.BlockCopy(append, 0, ret, 0, append.Length);
                return ret;
            }

            ret = new byte[orig.Length + append.Length];
            Buffer.BlockCopy(orig, 0, ret, 0, orig.Length);
            Buffer.BlockCopy(append, 0, ret, orig.Length, append.Length);
            return ret;
        }

        internal static byte[] ReadStream(int bufferSize, long contentLength, Stream stream)
        {
            if (contentLength < 1) return null;

            long bytesRemaining = contentLength;

            using (MemoryStream ms = new MemoryStream())
            {
                while (bytesRemaining > 0)
                {
                    byte[] buffer = null;
                    if (bytesRemaining >= bufferSize) buffer = new byte[bufferSize];
                    else buffer = new byte[bytesRemaining];

                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            ms.Write(buffer, 0, bytesRead);
                            bytesRemaining -= bytesRead;
                        }
                    }
                }

                return ms.ToArray();
            }
        }

        internal static double TotalMsFrom(DateTime startTime)
        {
            try
            {
                DateTime endTime = DateTime.Now;
                TimeSpan totalTime = (endTime - startTime);
                return totalTime.TotalMilliseconds;
            }
            catch (Exception)
            {
                return -1;
            }
        } 
    }
}
