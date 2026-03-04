using Modding;
using System;
using System.Reflection;
using GlobalEnums;
using Hkmp.Api.Client;
using Hkmp.Game;
using HkmpPouch;
using HKMirror.Reflection.SingletonClasses;

namespace SharedHealth
{
    internal class SharedHealth : Mod
    {
        internal static SharedHealth Instance { get; private set; }
        private PipeClient pipe = new ("SharedHealth");
        
        private const string HealEventName = "HealEvent";
        private const string DamageEventName = "DamageEvent";
        private const string BenchEventName = "BenchEvent";

        private bool isHealFromPipe = false;
        private bool isDamageFromPipe = false;
        private bool isBenchFromPipe = false;
        
        private static Loggable loggable = new SimpleLogger("SharedHealth");

        public SharedHealth() : base("SharedHealth") { }

        public override string GetVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public override void Initialize()
        {
            Instance = this;

            pipe.OnReady += SetupDamageAndHealHooks;

            Log("Initialized SharedHealth");
        }

        private void SetupDamageAndHealHooks(object sender, EventArgs e)
        {
            pipe.OnRecieve += ReceivePipeBroadcast;
            ModHooks.AfterTakeDamageHook += SendDamageInPipe;
            On.HeroController.TakeHealth += ResetIsDamageFromPipe;
            On.HeroController.AddHealth += SendHealthInPipe;
            On.HeroController.MaxHealth += SendBenchInPipe;
        }

        private void ReceivePipeBroadcast(object sender, ReceivedEventArgs e)
        {
            Team playerTeam = pipe.ClientApi.ClientManager.Team;
            Team receivedPipeTeam = (Team)e.Data.ExtraBytes[1];
            Log("Received pipe for " + e.Data.EventName + " from team " + receivedPipeTeam);

            if (playerTeam == Team.None)
            {
                Log("Ignoring pipe as player is in team None");
                return;
            }

            if (playerTeam != receivedPipeTeam)
            {
                Log("Ignoring pipe as player is not in the same team");
                return;
            }
            
            switch (e.Data.EventName)
            {
                case DamageEventName:
                    HandleDamageEvent(e);
                    break;
                case HealEventName:
                    HandleHealEvent(e);
                    break;
                case BenchEventName:
                    HandleBenchEvent(e);
                    break;
                default:
                    return;
            }
        }

        private void HandleDamageEvent(ReceivedEventArgs e)
        {
            Log("Handling damage event");
            byte damage = e.Data.ExtraBytes[0];
            Log("Applying " + damage + " damage");
            Log("Setting isDamageFromPipe to true");
            isDamageFromPipe = true;
            
            HeroController.instance.TakeHealth(damage);
        }

        private void HandleHealEvent(ReceivedEventArgs e)
        {
            Log("Start Heal Event Handling");
            byte health = e.Data.ExtraBytes[0];
            Log("Setting isHealFromPipe to true");
            isHealFromPipe = true;
            Log("Adding " + health + " health");
            HeroController.instance.AddHealth(health);
        }

        private void HandleBenchEvent(ReceivedEventArgs e)
        {
            Log("Handling bench event");

            isBenchFromPipe = true;
            
            HeroController.instance.MaxHealthKeepBlue();
            EventRegister.SendEvent("UPDATE BLUE HEALTH");
        }
        
        private void ResetIsDamageFromPipe(On.HeroController.orig_TakeHealth orig, HeroController self, int damageAmount)
        {
            orig(self, damageAmount);
            Log("Setting isDamageFromPipe to false in Reset");
            isDamageFromPipe = false;

            self.GetComponent<HeroAudioController>().PlaySound(HeroSounds.TAKE_HIT);
            
            if (self.playerData.health == 0)
            {
                Log("No more HP. Killing");

                _ = self.StartCoroutine(HeroControllerR.Die());
            }
        }
        
        private int SendDamageInPipe(int hazardType, int damageAmount)
        {
            Log("Detected damage, sending in pipe");

            if (isDamageFromPipe) return damageAmount;
            
            
            byte[] data = [HeroController.instance.playerData.overcharmed ? Convert.ToByte(damageAmount * 2) : Convert.ToByte(damageAmount), 
                (byte)pipe.ClientApi.ClientManager.Team];
            
            Log("Sending pipe for " + data[0] + " damage");
            pipe.Broadcast(DamageEventName, DamageEventName, data, false);
            
            Log("Setting isDamageFromPipe to false in Hook");
            isDamageFromPipe = false;
            
            return damageAmount;
        }

        private void SendHealthInPipe(On.HeroController.orig_AddHealth orig, HeroController self, int amount)
        {
            orig(self, amount);
            
            if (!isHealFromPipe)
            {
                Log("Heal not from pipe. Broadcast sent");

                pipe.Broadcast(HealEventName, HealEventName, [Convert.ToByte(amount), (byte)pipe.ClientApi.ClientManager.Team], false);
            } else Log("Heal from pipe. No broadcast");

            Log("Setting isHealFromPipe to false");
            isHealFromPipe = false;
        }

        private void SendBenchInPipe(On.HeroController.orig_MaxHealth orig, HeroController self)
        {
            orig(self);
            
            if (!isBenchFromPipe)
                pipe.Broadcast(BenchEventName, BenchEventName, [0, (byte)pipe.ClientApi.ClientManager.Team], false);

            isBenchFromPipe = false;
        }

        private new static void Log(string str)
        {
            // loggable.Log(str);
        }
    }
}