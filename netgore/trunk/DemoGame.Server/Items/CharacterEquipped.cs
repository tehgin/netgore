using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using DemoGame.Server.Queries;
using log4net;
using NetGore;

namespace DemoGame.Server
{
    public abstract class CharacterEquipped : EquippedBase<ItemEntity>, IDisposable, IModStatContainer
    {
        static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        readonly Character _character;

        readonly bool _isPersistent;

        /// <summary>
        /// Gets the Character that this UserEquipped belongs to.
        /// </summary>
        public Character Character
        {
            get { return _character; }
        }

        public DBController DBController
        {
            get { return Character.DBController; }
        }

        protected CharacterEquipped(Character character)
        {
            if (character == null)
                throw new ArgumentNullException("character");

            _character = character;
            _isPersistent = character.IsPersistent;

            AddListeners();
        }

        void AddListeners()
        {
            OnEquip += CharacterEquipped_OnEquip;
            OnRemove += CharacterEquipped_OnRemove;
        }

        /// <summary>
        /// When overridden in the derived class, checks if the given <paramref name="item"/> can be 
        /// equipped at all by the owner of this EquippedBase.
        /// </summary>
        /// <param name="item">Item to check if able be equip.</param>
        /// <returns>True if the <paramref name="item"/> can be equipped, else false.</returns>
        public override bool CanEquip(ItemEntity item)
        {
            return true;
        }

        /// <summary>
        /// When overridden in the derived class, checks if the item in the given <paramref name="slot"/> 
        /// can be removed properly.
        /// </summary>
        /// <param name="slot">Slot of the item to be removed.</param>
        /// <returns>True if the item can be properly removed, else false.</returns>
        protected override bool CanRemove(EquipmentSlot slot)
        {
            ItemEntityBase item = this[slot];
            if (item == null)
                return true;

            return Character.Inventory.CanAdd((ItemEntity)item);
        }

        void CharacterEquipped_OnEquip(EquippedBase<ItemEntity> equippedBase, ItemEntity item, EquipmentSlot slot)
        {
            Debug.Assert(item != null);

            if (_isPersistent)
            {
                InsertCharacterEquippedItemQuery.QueryArgs values = new InsertCharacterEquippedItemQuery.QueryArgs(Character.ID,
                                                                                                                   item.ID, slot);
                DBController.GetQuery<InsertCharacterEquippedItemQuery>().Execute(values);
            }

            SendSlotUpdate(slot, item.GraphicIndex);
        }

        void CharacterEquipped_OnRemove(EquippedBase<ItemEntity> equippedBase, ItemEntity item, EquipmentSlot slot)
        {
            Debug.Assert(item != null);

            if (_isPersistent)
                DBController.GetQuery<DeleteCharacterEquippedItemQuery>().Execute(item.ID);

            // Do not try working with a disposed item! Instead, just let it die off.
            if (item.IsDisposed)
                return;

            ItemEntity remainder = Character.Inventory.Add(item);

            SendSlotUpdate(slot, null);

            if (remainder != null)
            {
                const string errmsg =
                    "Character `{0}` removed equipped item `{1}` from slot `{2}`, " +
                    "but not all could be added back to their Inventory.";
                if (log.IsWarnEnabled)
                    log.WarnFormat(errmsg, Character, item, slot);

                // Make the Character drop the remainder
                Character.DropItem(remainder);
            }
        }

        /// <summary>
        /// Loads the Character's equipped items. The Character that this CharacterEquipped belongs to
        /// must be persistent since there is nothing for a non-persistent Character to load.
        /// </summary>
        public void Load()
        {
            if (!_isPersistent)
            {
                const string errmsg = "Don't call Load() when the Character's state is not persistent!";
                if (log.IsErrorEnabled)
                    log.Error(errmsg);
                Debug.Fail(errmsg);
                return;
            }

            var items = DBController.GetQuery<SelectCharacterEquippedItemsQuery>().Execute(Character.ID);

            // Remove the listeners since we don't want to update the database when loading
            RemoveListeners();

            // Load all the items
            foreach (var item in items)
            {
                ItemEntity itemEntity = new ItemEntity(item.Value);
                TrySetSlot(item.Key, itemEntity);
                SendSlotUpdate(item.Key, itemEntity.GraphicIndex);
            }

            // Add the listeners back
            AddListeners();
        }

        void RemoveListeners()
        {
            OnEquip -= CharacterEquipped_OnEquip;
            OnRemove -= CharacterEquipped_OnRemove;
        }

        protected virtual void SendSlotUpdate(EquipmentSlot slot, GrhIndex? graphicIndex)
        {
        }

        bool _disposed = false;

        #region IDisposable Members

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // If the Character is not persistent, we want to dispose of every item so it doesn't sit in the
            // database as garbage
            if (!_isPersistent)
            {
                foreach (var item in this.Select(x => x.Value))
                {
                    item.Dispose();
                }
            }
        }

        #endregion

        public int GetStatModBonus(StatType statType)
        {
            // TODO: [STATS] This totally sucks. Add some kind of cache.
            return this.SelectMany(x => x.Value.BaseStats).Where(x => x.StatType == statType).Select(x => x.Value).Sum();
        }
    }
}