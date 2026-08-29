using System;
using System.Collections.Generic;

using DevBoard.ViewModels;

namespace DevBoard.DevSpaces.Terminal
{
    public sealed class DevSpaceTerminalRegistry
    {
        public static DevSpaceTerminalRegistry Instance { get; } = new();

        public void Register(DevSpaceTerminal session)
        {
            ArgumentNullException.ThrowIfNull(session);

            lock (_gate)
                _sessions[session.Id] = session;
        }

        public bool Unregister(Guid id)
        {
            lock (_gate)
                return _sessions.Remove(id);
        }

        public bool TryGet(Guid id, out DevSpaceTerminal session)
        {
            lock (_gate)
                return _sessions.TryGetValue(id, out session);
        }

        public IReadOnlyList<string> GetDevSpaces()
        {
            lock (_gate)
            {
                var result = new List<string>();
                foreach (var session in _sessions.Values)
                {
                    var exists = false;
                    foreach (var item in result)
                    {
                        if (_devSpaceComparer.Equals(item, session.DevSpaceId))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                        result.Add(session.DevSpaceId);
                }

                return result.ToArray();
            }
        }

        public IReadOnlyList<DevSpaceTerminal> GetSessions(string devSpaceId = null)
        {
            lock (_gate)
            {
                var result = new List<DevSpaceTerminal>();
                foreach (var session in _sessions.Values)
                {
                    if (string.IsNullOrWhiteSpace(devSpaceId) ||
                        _devSpaceComparer.Equals(session.DevSpaceId, devSpaceId))
                    {
                        result.Add(session);
                    }
                }

                return result.ToArray();
            }
        }

        private static readonly StringComparer _devSpaceComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private readonly object _gate = new();
        private readonly Dictionary<Guid, DevSpaceTerminal> _sessions = [];
    }
}
