using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace GGQ.Client
{
    public class Activation : BaseScript
    {
        public Activation()
        {
            Tick += OnActivationTick;
        }

        public async Task OnActivationTick()
        {
            try
            {
                while(!NetworkIsPlayerActive(Game.Player.Handle))
                {
                    await Delay(1000);
                }

                TriggerServerEvent("ggq:playerActivated");
                Tick -= OnActivationTick;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[GGQ] ^1An error occurred while connecting. Please report this to the server administrator! Exception: {e.Message}^7");
                Debug.WriteLine("[GGQ] ^1Disconnecting now...^7");
                ExecuteCommand("disconnect");
            }
        }
    }
}
