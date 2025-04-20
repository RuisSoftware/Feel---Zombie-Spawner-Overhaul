using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zombiespawner.Sources.Scripts
{
    public class CmdSpawnZombies : ConsoleCmdAbstract
    {
        public override string getDescription()
        {
            return "Forceert het spawnen van extra virtuele zombies aan de maprand";
        }

        public override string getHelp()
        {
            return "Gebruik: spawnzombies <aantal>\nVoorbeeld: spawnzombies 5";
        }

        public override string[] getCommands()
        {
            return new string[] { "spawnzombies" };
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_params.Count != 1 || !int.TryParse(_params[0], out int count))
            {
                SdtdConsole.Instance.Output("Ongeldig gebruik. Typ 'spawnzombies <aantal>'");
                return;
            }

            GameManager.Instance.World.GetWorldExtent(out Vector3i min, out Vector3i max);
            for (int i = 0; i < count; i++)
            {
                var vz = VirtualZombieManager.CreateZombieForExternalCommand(min, max); // extern aan te roepen helper
                SdtdConsole.Instance.Output($"Virtuele zombie #{vz.id} gespawned op rand: {vz.startPos} -> {vz.destPos}");
            }
        }

    }

}
