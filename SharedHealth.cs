using Modding;
using System;
using System.Reflection;
using Hkmp.Api.Client;
using Hkmp.Game;
using HkmpPouch;

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
            On.HeroController.AddHealth += SendHealthInPipe;
            On.HeroController.MaxHealth += SendBenchInPipe;
        }

        private void ReceivePipeBroadcast(object sender, ReceivedEventArgs e)
        {
            IClientPlayer player = pipe.ClientApi.ClientManager.GetPlayer(e.Data.FromPlayer);

            if (pipe.ClientApi.ClientManager.Team != player.Team)
            {
                return;
            }

            if (player.Team == Team.None)
            {
                return;
            }
            
            switch (e.Data.EventName)
            {
                case DamageEventName:
                    HandleDamageEvent(e);
                    break;
                case HealEventName:
                case BenchEventName:
                    HandleHealEvent(e);
                    break;
                default:
                    return;
            }
        }

        private void HandleDamageEvent(ReceivedEventArgs e)
        {
            byte damage = e.Data.ExtraBytes[0];
            isDamageFromPipe = true;
            
            HeroController.instance.TakeHealth(damage);
        }

        private void HandleHealEvent(ReceivedEventArgs e)
        {
            byte health = e.Data.ExtraBytes[0];
            isHealFromPipe = true;
            
            HeroController.instance.AddHealth(health);
        }

        private int SendDamageInPipe(int hazardType, int damageAmount)
        {
            byte[] amountDamage = [Convert.ToByte(damageAmount)];
            
            if (!isDamageFromPipe)
                pipe.Broadcast(DamageEventName, DamageEventName, amountDamage, false);

            isDamageFromPipe = false;
            
            return damageAmount;
        }

        private void SendHealthInPipe(On.HeroController.orig_AddHealth orig, HeroController self, int amount)
        {
            orig(self, amount);
            
            byte[] amountHealed = [ Convert.ToByte(amount) ];
            
            if (!isHealFromPipe)
                pipe.Broadcast(HealEventName, HealEventName, amountHealed, false);

            isHealFromPipe = false;
        }

        private void SendBenchInPipe(On.HeroController.orig_MaxHealth orig, HeroController self)
        {
            orig(self);
            
            if (!isBenchFromPipe)
                pipe.Broadcast(BenchEventName, BenchEventName, [Byte.MaxValue], false);

            isBenchFromPipe = false;
        }
    }
}