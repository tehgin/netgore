using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NetGore;
using NetGore.Network;

namespace DemoGame.Server
{
    /// <summary>
    /// Handles tracking which slots of the UserInventory have changed, along with notifying the User of the
    /// new information for their inventory items.
    /// </summary>
    class UserInventoryUpdater
    {
        readonly BitArray _slotChanged = new BitArray(Inventory.MaxInventorySize);

        readonly UserInventory _userInventory;

        /// <summary>
        /// Gets the User to send the updates to.
        /// </summary>
        User OwnerUser
        {
            get { return UserInventory.Character as User; }
        }

        /// <summary>
        /// Gets the UserInventory that this UserInventoryUpdater manages.
        /// </summary>
        public UserInventory UserInventory
        {
            get { return _userInventory; }
        }

        /// <summary>
        /// UserInventoryUpdater constructor.
        /// </summary>
        /// <param name="userInventory">UserInventory that this UserInventoryUpdater will manage.</param>
        public UserInventoryUpdater(UserInventory userInventory)
        {
            if (userInventory == null)
                throw new ArgumentNullException("userInventory");

            _userInventory = userInventory;
        }

        /// <summary>
        /// Lets the UserInventoryUpdater know that an Inventory slot has changed and needs to be updated.
        /// </summary>
        /// <param name="slot">Slot that changed.</param>
        public void SlotChanged(InventorySlot slot)
        {
            _slotChanged[(int)slot] = true;
        }

        /// <summary>
        /// Updates the client with the changes that have been made to the UserInventory.
        /// </summary>
        public void Update()
        {
            // TODO: Recycle the PacketWriter instead of grabbing from the pool every time
            // Loop through all slots
            for (InventorySlot slot = new InventorySlot(0); slot < Inventory.MaxInventorySize; slot++)
            {
                // Skip unchanged slots
                if (!_slotChanged[(int)slot])
                    continue;

                // Get the item in the slot
                ItemEntity item = UserInventory[slot];

                if (item == null)
                {
                    // Remove the item
                    using (PacketWriter pw = ServerPacket.SetInventorySlot(slot, new GrhIndex(0), 0))
                    {
                        OwnerUser.Send(pw);
                    }
                }
                else
                {
                    // Update the item
                    using (PacketWriter pw = ServerPacket.SetInventorySlot(slot, item.GraphicIndex, item.Amount))
                    {
                        OwnerUser.Send(pw);
                    }
                }
            }

            // Changes complete
            _slotChanged.SetAll(false);
        }
    }
}