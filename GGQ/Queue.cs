using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Dynamic;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using GGQ.Models;

namespace GGQ.Server
{
    public class Queue : BaseScript
    {
        public List<QueuePlayer> m_queue = new List<QueuePlayer>();
        private Config m_config = new Config(); // for now

        public Queue() { }

        [Tick]
        public async Task ProcessQueue()
        {
            try
            {
                foreach (QueuePlayer queuePlayer in m_queue.Where(p => p.Status != QueueStatus.Connecting).ToList())
                {
                    // Let in player if first in queue and server has a slot
                    if (m_queue.IndexOf(queuePlayer) == 0 && this.Players.Count() < m_config.MaxClients)
                    {
                        ((CallbackDelegate)queuePlayer.Deferrals?.ToList()[1].Value)?.Invoke();
                        queuePlayer.Status = QueueStatus.Connecting;
                        queuePlayer.ConnectTime = DateTime.UtcNow;
                        Log($"Letting in player: {queuePlayer.Name}");
                        continue;
                    }
                    // Defer the player until there is a slot available and they're first in queue.
                    if (queuePlayer.Status != QueueStatus.Queued) continue;
                    ((CallbackDelegate)queuePlayer.Deferrals.ToList()[2].Value)(
                        $"[{m_queue.IndexOf(queuePlayer) + 1}/{m_queue.Count}] In queue to connect.{queuePlayer.Dots}");
                    queuePlayer.Dots = new string('.', (queuePlayer.Dots.Length + 1) % 3);
                }
                // Remove players who have been disconnected longer than the grace period
                foreach (QueuePlayer queuePlayer in m_queue.Where(p => p.Status == QueueStatus.Disconnected && DateTime.UtcNow.Subtract(p.DisconnectTime).TotalSeconds > m_config.DisconnectGrace).ToList())
                {
                    Log($"Disconnect grace expired for player: {queuePlayer.Name}  {queuePlayer.SteamId}");
                    m_queue.Remove(queuePlayer);
                }
                // Remove players who have been connecting longer than the connect timeout
                foreach (QueuePlayer queuePlayer in m_queue.Where(p => p.Status == QueueStatus.Connecting && DateTime.UtcNow.Subtract(p.ConnectTime).TotalSeconds > m_config.ConnectionTimeout).ToList())
                {
                    Log($"Connect timeout expired for player: {queuePlayer.Name}  {queuePlayer.SteamId}");
                    m_queue.Remove(queuePlayer);
                }
                // Remove players who have timed out
                foreach (QueuePlayer queuePlayer in m_queue.Where(p => p.Status != QueueStatus.Disconnected && GetPlayerLastMsg(p.Handle.ToString()) > TimeSpan.FromSeconds(m_config.ConnectionTimeout).TotalMilliseconds).ToList())
                {
                    OnPlayerDisconnect(m_queue.First(p => p.SteamId == queuePlayer.SteamId), "Timed out");
                }
                // Update the servername
                SetConvar("sv_hostname", m_queue.Count > 0 ? $"[Q: {m_queue.Count}] {m_config.HostName}" : m_config.HostName);
            }
            catch (Exception e)
            {
                Log(e.Message);
                Log(e.InnerException?.Message);
                Log(e.StackTrace);
            }

            await Delay(m_config.DeferralDelay);
        }

        [EventHandler("playerConnecting")]
        public void OnPlayerConnecting([FromSource] Player player, string name, CallbackDelegate kickReason, ExpandoObject deferrals)
        {
            try
            {
                Log($"Connecting: {player.Name}  {player.Identifiers["license"]}");
                // Check if in queue
                QueuePlayer queuePlayer = m_queue.FirstOrDefault(p => p.LicenseId == player.Identifiers["license"]);

                if (queuePlayer != null)
                {
                    // Player had a slot in the queue, give them it back.
                    Log($"Player found in queue: {queuePlayer.Name}");
                    queuePlayer.Handle = int.Parse(player.Handle);
                    queuePlayer.Status = QueueStatus.Queued;
                    queuePlayer.ConnectTime = new DateTime();
                    queuePlayer.JoinCount++;
                    queuePlayer.Deferrals = deferrals;

                    ((CallbackDelegate)queuePlayer.Deferrals.ToList()[0].Value)();
                    ((CallbackDelegate)queuePlayer.Deferrals.ToList()[2].Value)("Connecting");

                    return;
                }

                // Slot available, don't bother with the queue.
                if (this.Players.Count() < m_config.MaxClients && !m_config.QueueWhenNotFull) return;

                var licenseId = player.Identifiers["license"];
                var steamId = player.Identifiers["steam"];
                var xblId = player.Identifiers["xbl"];
                var liveId = player.Identifiers["live"];
                var discordId = player.Identifiers["discord"];
                var fivemId = player.Identifiers["fivem"];

                // Check if the player is in the priority list
                // NEEDS REWORK
                PriorityPlayer priorityPlayer = m_config.PriorityPlayers.FirstOrDefault(
                    p => p.SteamId == steamId || p.LicenseId == licenseId || 
                    p.DiscordId == discordId || p.FivemId == fivemId);


                    
                // Add to queue
                queuePlayer = new QueuePlayer()
                {
                    Handle = int.Parse(player.Handle),
                    LicenseId = licenseId,
                    SteamId = steamId,
                    XblId = xblId,
                    LiveId = liveId,
                    DiscordId = discordId,
                    FivemId = fivemId,
                    Name = player.Name,
                    JoinCount = 1,
                    JoinTime = DateTime.UtcNow,
                    Deferrals = deferrals,
                    Priority = priorityPlayer?.Priority ?? 100
                };

                AddToQueue(queuePlayer);
            }
            catch (Exception e)
            {
                Log(e.Message);
                CancelEvent();
            }
        }

        private void AddToQueue(QueuePlayer player)
        {
            // Find out where to insert them in the queue.
            int queuePosition = m_queue.FindLastIndex(p => p.Priority <= player.Priority) + 1;

            m_queue.Insert(queuePosition, player);

            Log($"Added {player.Name} to the queue with priority {player.Priority} [{m_queue.IndexOf(player) + 1}/{m_queue.Count}]");
            if (player.Deferrals == null) return;
            ((CallbackDelegate)player.Deferrals.ToList()[0].Value)();
            ((CallbackDelegate)player.Deferrals.ToList()[2].Value)("Connecting");
        }

        [EventHandler("playerDropped")]
        public void OnPlayerDropped([FromSource] Player player, string disconnectMessage, CallbackDelegate kickReason)
        {
            try
            {
                QueuePlayer queuePlayer = m_queue.FirstOrDefault(p => p.LicenseId == player.Identifiers["license"]);
                if (queuePlayer == null) return;
                OnPlayerDisconnect(queuePlayer, disconnectMessage);
            }
            catch (Exception e)
            {
                Log(e.Message);
            }
        }

        [EventHandler("ggq:playerConnected")]
        public void OnPlayerConnected([FromSource] Player player)
        {
            Log($"Player connected, removing from queue: {player.Name}");
            try
            {
                QueuePlayer queuePlayer = m_queue.FirstOrDefault(p => p.LicenseId == player.Identifiers["license"]);
                if (queuePlayer != null) m_queue.Remove(queuePlayer);
            }
            catch (Exception e)
            {
                Log(e.Message);
            }
        }

        private void OnPlayerDisconnect(QueuePlayer queuePlayer, string disconnectMessage)
        {
            Log($"Disconnected: {queuePlayer.Name} {disconnectMessage}");
            queuePlayer.Status = QueueStatus.Disconnected;
            queuePlayer.DisconnectTime = DateTime.UtcNow;
        }

        public static void Log(string message)
        {
            Debug.WriteLine($"{DateTime.Now:s} [SERVER:QUEUE]: {message}");
        }
    }
}
