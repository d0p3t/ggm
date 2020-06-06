using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace GGQ.Client
{
    public class Activation : BaseScript
    {
        private static bool IsConnected { get; set; } = false;

        public Activation()
        {
            // Empty constructor
        }

        [Tick]
        public async Task OnActivationTick()
        {
            try
            {
                if(NetworkIsSessionStarted())
                {
                    if(!IsConnected)
                    {
                        IsConnected = true;
                        TriggerServerEvent("ggq:playerConnected");
                        Tick -= OnActivationTick;
                    }
                }

                await Delay(1);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[GGQ] ^1An error occurred while connecting. Please report this to the server administrator! Exception: {e.Message}^7");
                ExecuteCommand("disconnect");
            }
        }
    }
}
