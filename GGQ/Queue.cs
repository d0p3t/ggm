using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace GGQ.Server
{
    public class Queue : BaseScript
    {
        internal static string resourceName = GetCurrentResourceName();
        internal static string resourcePath = $"resources/{GetResourcePath(resourceName).Substring(GetResourcePath(resourceName).LastIndexOf("//") + 2)}";
        private ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> newQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> pQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> newPQueue = new ConcurrentQueue<string>();
        private ConcurrentDictionary<string, SessionState> session = new ConcurrentDictionary<string, SessionState>();
        private ConcurrentDictionary<string, int> index = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, DateTime> timer = new ConcurrentDictionary<string, DateTime>();
        private ConcurrentDictionary<string, Player> sentLoading = new ConcurrentDictionary<string, Player>();
        internal static ConcurrentDictionary<string, int> priority = new ConcurrentDictionary<string, int>();
        internal static ConcurrentDictionary<string, Reserved> reserved = new ConcurrentDictionary<string, Reserved>();
        internal static ConcurrentDictionary<string, Reserved> slotTaken = new ConcurrentDictionary<string, Reserved>();
        private string allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ~`!@#$%&*()_-+={[}]|:;<,>.?/\\";

        private bool allowSymbols = true;
        private int maxSession = 32;
        private double queueGraceTime = 2;
        private double graceTime = 3;
        private int inQueue = 0;
        private int inPriorityQueue = 0;
        private string originalHostName = string.Empty;
        private int lastCount = 0;
        private bool serverQueueReady = false;
        private bool stateChangeMessages = false;

        public Queue()
        {

        }

        [EventHandler("onResourceStart")]
        public void OnResourceStart(string resourceName)
        {
            if (GetCurrentResourceName() != resourceName)
            {
                return;
            }

            originalHostName = GetConvar("sv_hostname", string.Empty);
            maxSession = GetConvarInt("sv_maxclients", 32);
            stateChangeMessages = GetConvar("ggq_debug", "true") == "true";

            serverQueueReady = true;

            Debug.WriteLine("[GGQ] ^2Server queue ready^7");
        }

        [EventHandler("onResourceStop")]
        public void OnResourceStop(string resourceName)
        {
            if(GetCurrentResourceName() != resourceName)
            {
                return;
            }

            if(originalHostName != string.Empty)
            {
                SetConvar("sv_hostname", originalHostName);
            }
        }

        [EventHandler("playerConnecting")]
        public async void OnPlayerConnecting([FromSource]Player source, string playerName, dynamic kickReason, dynamic deferrals)
        {
            try
            {
                deferrals.defer();
                await Delay(500);
                while (!IsEverythingReady()) { await Delay(0); }
                deferrals.update("Connecting you to Gun Game");
                string license = source.Identifiers["license"];
                if (license == null) { deferrals.done("Could not find your license identifier."); return; }

                if (!allowSymbols && !ValidName(playerName)) { deferrals.done("No symbols or consecutive spaces are allowed in your name"); return; }

                bool banned = false;
                string reason = string.Empty;
                DateTime endDate = DateTime.UtcNow;
                int id = 0;

                //dynamic banInfo = Exports["ggsql"].CheckBanned(source.Handle);

                //if(banInfo != null && banInfo.IsBanned)
                //{
                //    banned = true;
                //    reason = banInfo.Reason;
                //    endDate = banInfo.EndDate;
                //    id = banInfo.Id;
                //}

                if (banned)
                {
                    deferrals.done($"You're banned. \n\nUntil: {endDate.ToLongDateString()} {endDate.ToLongTimeString()}\nReason: {reason}\nID: {id}");
                    RemoveFrom(license, true, true, true, true, true, true);
                    return;
                }

                if (sentLoading.ContainsKey(license))
                {
                    sentLoading.TryRemove(license, out Player oldPlayer);
                }
                sentLoading.TryAdd(license, source);

                if (session.TryAdd(license, SessionState.Queue))
                {
                    if (!priority.ContainsKey(license))
                    {
                        newQueue.Enqueue(license);
                        if (stateChangeMessages) { Debug.WriteLine($"[GGQ]: NEW -> QUEUE -> (Public) {license}"); }
                    }
                    else
                    {
                        newPQueue.Enqueue(license);
                        if (stateChangeMessages) { Debug.WriteLine($"[GGQ]: NEW -> QUEUE -> (Priority) {license}"); }
                    }
                }

                if (!session[license].Equals(SessionState.Queue))
                {
                    UpdateTimer(license);
                    session.TryGetValue(license, out SessionState oldState);
                    session.TryUpdate(license, SessionState.Loading, oldState);
                    deferrals.done();
                    if (stateChangeMessages) { Debug.WriteLine($"[GGQ]: {Enum.GetName(typeof(SessionState), oldState).ToUpper()} -> LOADING -> (Grace) {license}"); }
                    return;
                }

                bool inPriority = priority.ContainsKey(license);
                int dots = 0;

                while (session[license].Equals(SessionState.Queue))
                {
                    if (index.ContainsKey(license) && index.TryGetValue(license, out int position))
                    {
                        int count = inPriority ? inPriorityQueue : inQueue;
                        string message = inPriority ? "You are in priority queue" : "You are in queue";
                        deferrals.update($"{message} {position} / {count}{new string('.', dots)}");
                    }
                    dots = dots > 2 ? 0 : dots + 1;
                    if (source?.EndPoint == null)
                    {
                        UpdateTimer(license);
                        deferrals.done("Lost connection. Try again.");
                        if (stateChangeMessages) { Debug.WriteLine($"[GGQ]: QUEUE -> CANCELED -> {license}"); }
                        return;
                    }
                    RemoveFrom(license, false, false, true, false, false, false);
                    await Delay(5000);
                }
                await Delay(500);
                deferrals.done();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                deferrals.done($"Error"); return;
            }
        }

        [EventHandler("playerDropped")]
        public void OnPlayerDropped([FromSource]Player player, string reason)
        {
            try
            {
                string license = player.Identifiers["license"];
                if (license == null)
                {
                    return;
                }
                if (!session.ContainsKey(license) || reason == "Exited")
                {
                    return;
                }
                bool hasState = session.TryGetValue(license, out SessionState oldState);
                if (hasState && oldState != SessionState.Queue)
                {
                    session.TryUpdate(license, SessionState.Grace, oldState);
                    if (stateChangeMessages) { Debug.WriteLine($"[{resourceName}]: {Enum.GetName(typeof(SessionState), oldState).ToUpper()} -> GRACE -> {license}"); }
                    UpdateTimer(license);
                }
            }
            catch (Exception)
            {
                Debug.WriteLine($"[{resourceName} - ERROR] - OnPlayerDropped()");
            }
        }

        [EventHandler("ggq:playerActivated")]
        public void OnPlayerActivated([FromSource]Player player)
        {
            try
            {
                string license = player.Identifiers["license"];
                if (!session.ContainsKey(license))
                {
                    session.TryAdd(license, SessionState.Active);
                    return;
                }
                session.TryGetValue(license, out SessionState oldState);
                session.TryUpdate(license, SessionState.Active, oldState);
                if (stateChangeMessages) { Debug.WriteLine($"[{resourceName}]: {Enum.GetName(typeof(SessionState), oldState).ToUpper()} -> ACTIVE -> {license}"); }
            }
            catch (Exception)
            {
                Debug.WriteLine($"[{resourceName} - ERROR] - OnPlayerActivated()");
            }
        }

        [Tick]
        public async Task OnQueueTick()
        {
            try
            {
                inQueue = QueueCount();
                await Delay(100);
                UpdateHostName();
                UpdateStates();
                await Delay(1000);
                
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[GGQ] ^1ERROR^7 - OnQueueTick() - {e.Message}");
            }
        }

        private bool ValidName(string playerName)
        {
            char[] chars = playerName.ToCharArray();

            char lastCharacter = new char();
            foreach (char currentCharacter in chars)
            {
                if (!allowedChars.ToCharArray().Contains(currentCharacter)) { return false; }
                if (char.IsWhiteSpace(currentCharacter) && char.IsWhiteSpace(lastCharacter)) { return false; }
                lastCharacter = currentCharacter;
            }
            return true;
        }

        private int QueueCount()
        {
            try
            {
                int place = 0;
                ConcurrentQueue<string> temp = new ConcurrentQueue<string>();
                while (!queue.IsEmpty)
                {
                    queue.TryDequeue(out string license);
                    if (IsTimeUp(license, queueGraceTime))
                    {
                        RemoveFrom(license, true, true, true, true, true, true);
                        if (stateChangeMessages) { Debug.WriteLine($"[GGQ]: CANCELED -> REMOVED -> {license}"); }
                        continue;
                    }

                    if (!Loading(license))
                    {
                        place += 1;
                        UpdatePlace(license, place);
                        temp.Enqueue(license);
                    }
                }
                while (!newQueue.IsEmpty)
                {
                    newQueue.TryDequeue(out string license);
                    if (!Loading(license))
                    {
                        place += 1;
                        UpdatePlace(license, place);
                        temp.Enqueue(license);
                    }
                }
                queue = temp;
                return queue.Count;
            }
            catch (Exception)
            {
                Debug.WriteLine($"[GGQ] ^1ERROR^7 - QueueCount()");
                return queue.Count;
            }
        }

        private void UpdateHostName()
        {
            try
            {
                if (originalHostName == string.Empty) { originalHostName = GetConvar("sv_hostname", string.Empty); }
                if (originalHostName == string.Empty) { return; }

                string concat = originalHostName;
                bool editHost = false;
                int count = inQueue + inPriorityQueue;

                editHost = true;
                if (count > 0) { concat = string.Format($"[Queue: {0}] {concat}", count); }
                else { concat = originalHostName; }

                if (lastCount != count && editHost)
                {
                    SetConvar("sv_hostname", concat);
                }
                lastCount = count;
            }
            catch (Exception)
            {
                Debug.WriteLine($"[{resourceName} - ERROR] - UpdateHostName()");
            }
        }

        private bool Loading(string license)
        {
            try
            {
                if (session.Count(j => j.Value != SessionState.Queue) - slotTaken.Count(i => i.Value != Reserved.Public) < maxSession)
                { NewLoading(license, Reserved.Public); return true; }
                else { return false; }
            }
            catch (Exception)
            {
                Debug.WriteLine($"[{resourceName} - ERROR] - Loading()"); return false;
            }
        }

        private void NewLoading(string license, Reserved slotType)
        {
            try
            {
                if (session.TryGetValue(license, out SessionState oldState))
                {
                    UpdateTimer(license);
                    RemoveFrom(license, false, true, false, false, false, false);
                    if (!slotTaken.TryAdd(license, slotType))
                    {
                        slotTaken.TryGetValue(license, out Reserved oldSlotType);
                        slotTaken.TryUpdate(license, slotType, oldSlotType);
                    }
                    session.TryUpdate(license, SessionState.Loading, oldState);
                    if (stateChangeMessages) { Debug.WriteLine($"[{resourceName}]: QUEUE -> LOADING -> ({Enum.GetName(typeof(Reserved), slotType)}) {license}"); }
                }
            }
            catch (Exception)
            {
                Debug.WriteLine($"[GGQ] ^1ERROR^7 - NewLoading()");
            }
        }

        private bool IsTimeUp(string license, double time)
        {
            try
            {
                if (!timer.ContainsKey(license)) { return false; }
                return timer[license].AddMinutes(time) < DateTime.UtcNow;
            }
            catch (Exception)
            {
                Debug.WriteLine($"[GGQ] ^1ERROR^7 - IsTimeUp()"); return false;
            }
        }

        private void UpdatePlace(string license, int place)
        {
            try
            {
                if (!index.TryAdd(license, place))
                {
                    index.TryGetValue(license, out int oldPlace);
                    index.TryUpdate(license, place, oldPlace);
                }
            }
            catch (Exception)
            {
                Debug.WriteLine($"[GGQ] ^1ERROR^7 - UpdatePlace()");
            }
        }

        private void UpdateTimer(string license)
        {
            try
            {
                if (!timer.TryAdd(license, DateTime.UtcNow))
                {
                    timer.TryGetValue(license, out DateTime oldTime);
                    timer.TryUpdate(license, DateTime.UtcNow, oldTime);
                }
            }
            catch (Exception)
            {
                Debug.WriteLine($"[GGQ] ^1ERROR^7 - UpdateTimer()");
            }
        }

        private void UpdateStates()
        {
            try
            {
                session.Where(k => k.Value == SessionState.Loading || k.Value == SessionState.Grace).ToList().ForEach(j =>
                {
                    string license = j.Key;
                    SessionState state = j.Value;
                    switch (state)
                    {
                        case SessionState.Loading:
                            if (!timer.TryGetValue(license, out DateTime oldLoadTime))
                            {
                                UpdateTimer(license);
                                break;
                            }

                            if (sentLoading.ContainsKey(license) && Players.FirstOrDefault(i => i.Identifiers["license"] == license) != null)
                            {
                                sentLoading.TryRemove(license, out Player oldPlayer);
                            }

                            break;
                        case SessionState.Grace:
                            if (!timer.TryGetValue(license, out DateTime oldGraceTime))
                            {
                                UpdateTimer(license);
                                break;
                            }
                            if (IsTimeUp(license, graceTime))
                            {
                                if (Players.FirstOrDefault(i => i.Identifiers["license"] == license)?.EndPoint != null)
                                {
                                    if (!session.TryAdd(license, SessionState.Active))
                                    {
                                        session.TryGetValue(license, out SessionState oldState);
                                        session.TryUpdate(license, SessionState.Active, oldState);
                                    }
                                }
                                else
                                {
                                    RemoveFrom(license, true, true, true, true, true, true);
                                    if (stateChangeMessages) { Debug.WriteLine($"[{resourceName}]: GRACE -> REMOVED -> {license}"); }
                                }
                            }
                            break;
                    }
                });
            }
            catch (Exception)
            {
                Debug.WriteLine($"[{resourceName} - ERROR] - UpdateStates()");
            }
        }

        private void RemoveFrom(string license, bool doSession, bool doIndex, bool doTimer, bool doPriority, bool doReserved, bool doSlot)
        {
            try
            {
                if (doSession) { session.TryRemove(license, out SessionState oldState); }
                if (doIndex) { index.TryRemove(license, out int oldPosition); }
                if (doTimer) { timer.TryRemove(license, out DateTime oldTime); }
                if (doPriority) { priority.TryRemove(license, out int oldPriority); }
                if (doReserved) { reserved.TryRemove(license, out Reserved oldReserved); }
                if (doSlot) { slotTaken.TryRemove(license, out Reserved oldSlot); }
            }
            catch (Exception)
            {
                Debug.WriteLine($"[GGQ] ^1ERROR^7 - RemoveFrom()");
            }
        }

        private bool IsEverythingReady()
        {
            if (serverQueueReady)
            { return true; }
            return false;
        }
    }

    enum SessionState
    {
        Queue,
        Grace,
        Loading,
        Active,
    }

    enum Reserved
    {
        Reserved1 = 1,
        Reserved2,
        Reserved3,
        Public
    }
}
