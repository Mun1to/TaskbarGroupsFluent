#nullable enable annotations

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// The one-line protocol between the hover watcher and a warm flyout.
    ///
    /// A flyout that has already loaded can be put on screen in about fifty
    /// milliseconds, against roughly nine hundred for one started from scratch, so
    /// the watcher keeps one loaded and hidden while the cursor is near the taskbar
    /// and tells it what to show. This is that telling: a named pipe, because it is
    /// the plainest thing that carries a short message between two processes of the
    /// same user without polling anything.
    ///
    /// Messages go one way and are not answered. Answering them was tried, to tell
    /// "the pipe took my bytes" from "the panel is up", and it made things worse: a
    /// reply read back on the same pipe stalled often enough to lose showings
    /// outright. The real problem was asking too early, and that is solved at the
    /// source instead, by a Ready handle the flyout raises once it is listening.
    /// </summary>
    public static class FlyoutChannel
    {
        /// <summary>
        /// Pipe name. Per-user so two people signed in at once do not talk to each
        /// other's flyout.
        /// </summary>
        public static string PipeName =>
            "TaskbarGroupsFluent.Flyout." + Environment.UserName;

        /// <summary>
        /// Name of the handle a warm flyout raises once it is loaded and listening.
        /// Asking before that is what used to lose the first showing after a start.
        /// </summary>
        public static string ReadyName => "TaskbarGroupsFluent.FlyoutReady." + Environment.UserName;

        /// <summary>
        /// Asks a warm flyout to show a group. Returns false if the message could
        /// not be delivered, which is the caller's cue to start a flyout the ordinary
        /// way. Only worth calling once IsFlyoutReady is true.
        /// </summary>
        public static bool Show(string group, int left, int top, int right, int bottom, int timeoutMs = 300)
            => Send($"show\t{group}\t{left}\t{top}\t{right}\t{bottom}", timeoutMs);

        /// <summary>
        /// True if a warm flyout has finished loading and is listening. This is what
        /// keeps the watcher from talking to one that is still starting up, which
        /// used to swallow the first showing after every start.
        /// </summary>
        public static bool IsFlyoutReady()
        {
            try
            {
                if (!EventWaitHandle.TryOpenExisting(ReadyName, out EventWaitHandle? handle)
                    || handle is null)
                    return false;

                using (handle) return handle.WaitOne(0);
            }
            catch { return false; }
        }

        /// <summary>
        /// Raises the handle that says this process is listening. Held for the life
        /// of the flyout; releasing it sends the watcher back to starting its own.
        /// </summary>
        public static EventWaitHandle CreateReady()
            => new EventWaitHandle(true, EventResetMode.ManualReset, ReadyName);

        /// <summary>Asks a warm flyout to put its panel away but stay loaded.</summary>
        public static bool Hide(int timeoutMs = 200) => Send("hide", timeoutMs);

        /// <summary>Asks a warm flyout to exit. Best effort.</summary>
        public static bool Quit(int timeoutMs = 200) => Send("quit", timeoutMs);

        private static bool Send(string message, int timeoutMs)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                pipe.Connect(timeoutMs);

                byte[] payload = Encoding.UTF8.GetBytes(message);
                pipe.Write(payload, 0, payload.Length);
                pipe.Flush();
                return true;
            }
            catch
            {
                // Nobody listening, or it died mid-send. Either way the caller falls
                // back to launching a flyout of its own.
                return false;
            }
        }

        /// <summary>
        /// Listens for messages until <paramref name="stop"/> is signalled, handing
        /// each to <paramref name="onMessage"/>. Runs on its own thread, so a UI
        /// caller has to marshal.
        /// </summary>
        public static void Listen(Action<string> onMessage, CancellationToken stop)
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    pipe.WaitForConnectionAsync(stop).GetAwaiter().GetResult();
                    if (stop.IsCancellationRequested) return;

                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    string? line = reader.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(line)) onMessage(line.Trim());
                }
                catch (OperationCanceledException) { return; }
                catch
                {
                    // A malformed or interrupted connection must not end the loop;
                    // the flyout would go deaf and silently stop being warm.
                    if (stop.IsCancellationRequested) return;
                    Thread.Sleep(50);
                }
            }
        }

        /// <summary>
        /// Reads a "show" message. Returns false for anything else or malformed.
        /// </summary>
        public static bool TryParseShow(string message, out string group,
            out int left, out int top, out int right, out int bottom)
        {
            group = string.Empty;
            left = top = right = bottom = 0;

            string[] parts = message.Split('\t');
            if (parts.Length != 6 || parts[0] != "show") return false;

            group = parts[1];
            return group.Length > 0
                && int.TryParse(parts[2], out left)
                && int.TryParse(parts[3], out top)
                && int.TryParse(parts[4], out right)
                && int.TryParse(parts[5], out bottom);
        }
    }
}
