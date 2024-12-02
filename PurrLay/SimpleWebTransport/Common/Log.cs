using Conditional = System.Diagnostics.ConditionalAttribute;

namespace JamesFrowen.SimpleWeb
{
    public static class Log
    {
        // used for Conditional
        const string SIMPLEWEB_LOG_ENABLED = nameof(SIMPLEWEB_LOG_ENABLED);
        const string DEBUG = nameof(DEBUG);

        public enum Levels
        {
            none = 0,
            error = 1,
            warn = 2,
            info = 3,
            verbose = 4,
        }

        public static Levels level = Levels.none;

        public static string BufferToString(byte[] buffer, int offset = 0, int? length = null)
        {
            return BitConverter.ToString(buffer, offset, length ?? buffer.Length);
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void DumpBuffer(string label, byte[] buffer, int offset, int length)
        {
            if (level < Levels.verbose)
                return;

            Console.WriteLine($"VERBOSE: {label}: {BufferToString(buffer, offset, length)}");
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void DumpBuffer(string label, ArrayBuffer arrayBuffer)
        {
            if (level < Levels.verbose)
                return;

            Console.WriteLine($"VERBOSE: {label}: {BufferToString(arrayBuffer.array, 0, arrayBuffer.count)}");
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void Verbose(string msg, bool showColor = true)
        {
            if (level < Levels.verbose)
                return;

            Console.WriteLine(showColor ? $"VERBOSE: <color=cyan>{msg}</color>" : $"VERBOSE: {msg}");
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void Info(string msg, bool showColor = true)
        {
            if (level < Levels.info)
                return;

            Console.WriteLine(showColor ? $"INFO: <color=cyan>{msg}</color>" : $"INFO: {msg}");
        }

        /// <summary>
        /// An expected Exception was caught, useful for debugging but not important
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="showColor"></param>
        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void InfoException(Exception e)
        {
            if (level < Levels.info)
                return;

            Console.WriteLine($"INFO_EXCEPTION: <color=cyan>{e.GetType().Name}</color> Message: {e.Message}\n{e.StackTrace}\n\n");
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED), Conditional(DEBUG)]
        public static void Warn(string msg, bool showColor = true)
        {
            if (level < Levels.warn)
                return;

            Console.WriteLine(showColor ? $"WARN: <color=orange>{msg}</color>" : $"WARN: {msg}");
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED), Conditional(DEBUG)]
        public static void Error(string msg, bool showColor = true)
        {
            if (level < Levels.error)
                return;

            Console.WriteLine(showColor ? $"ERROR: <color=red>{msg}</color>" : $"ERROR: {msg}");
        }

        public static void Exception(Exception e)
        {
            Console.WriteLine($"EXCEPTION: <color=red>{e.GetType().Name}</color> Message: {e.Message}\n{e.StackTrace}\n\n");
        }
    }
}
