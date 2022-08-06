using SecretHistories.Abstract;
using SecretHistories.Commands.SituationCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Roost.World.Shelves
{
    class ShelfCreationCommand : ITokenPayloadCreationCommand, IEncaustment
    {
        public string Id { get; set; }

        public string EntityId { get; set; }
        public int Quantity { get; set; }
        public List<PopulateDominionCommand> Dominions { get; set; }

        public ShelfCreationCommand(){}
        public ShelfCreationCommand(string entityId)
        {
            Dominions = new List<PopulateDominionCommand>();
            EntityId = entityId;
            Id = $"shelf_{entityId}";
        }

        public ITokenPayload Execute(Context context)
        {
            var z = new ShelfPayload(Id, EntityId);
            return z;
        }
    }
}
