using System.Threading;

namespace HttpServerLite.Test
{
    public class TestHelper
    {
        private static int _portCounter = 1200;
        public static ushort GetPort() => (ushort)Interlocked.Increment(ref _portCounter);
    }
}