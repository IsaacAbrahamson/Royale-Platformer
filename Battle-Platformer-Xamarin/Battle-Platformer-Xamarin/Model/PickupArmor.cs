﻿using Urho;
using Urho.Urho2D;

namespace Royale_Platformer.Model
{
    public class PickupArmor : Pickup
    {
        public PickupArmor() : base()
        {

        }

        public PickupArmor(Scene scene, Sprite2D sprite, Vector2 pos) : base(scene, sprite, pos)
        {
        }

        public override ISerializer Deserialize(string serialized)
        {
            throw new System.NotImplementedException();
        }

        public override bool PickUp(Character character)
        {
            if (character.Armor) return false;

            character.Armor = true;
            return true;
        }
    }
}
