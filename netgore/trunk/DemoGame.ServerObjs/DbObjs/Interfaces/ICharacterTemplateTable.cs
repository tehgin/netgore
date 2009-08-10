using System;
using System.Collections.Generic;
using System.Linq;

namespace DemoGame.Server.DbObjs
{
    /// <summary>
    /// Interface for a class that can be used to serialize values to the database table `character_template`.
    /// </summary>
    public interface ICharacterTemplateTable
    {
        /// <summary>
        /// Gets the value of the database column `ai`.
        /// </summary>
        String AI { get; }

        /// <summary>
        /// Gets the value of the database column `alliance_id`.
        /// </summary>
        AllianceID AllianceID { get; }

        /// <summary>
        /// Gets the value of the database column `body_id`.
        /// </summary>
        BodyIndex BodyID { get; }

        /// <summary>
        /// Gets the value of the database column `exp`.
        /// </summary>
        UInt32 Exp { get; }

        /// <summary>
        /// Gets the value of the database column `give_cash`.
        /// </summary>
        UInt16 GiveCash { get; }

        /// <summary>
        /// Gets the value of the database column `give_exp`.
        /// </summary>
        UInt16 GiveExp { get; }

        /// <summary>
        /// Gets the value of the database column `id`.
        /// </summary>
        UInt16 ID { get; }

        /// <summary>
        /// Gets the value of the database column `level`.
        /// </summary>
        Byte Level { get; }

        /// <summary>
        /// Gets the value of the database column `name`.
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Gets the value of the database column `respawn`.
        /// </summary>
        UInt16 Respawn { get; }

        /// <summary>
        /// Gets the value of the database column `statpoints`.
        /// </summary>
        UInt32 StatPoints { get; }

        IEnumerable<KeyValuePair<StatType, Int32>> Stats { get; }

        /// <summary>
        /// Gets the value of the database column in the column collection `{0}`
        /// that corresponds to the given <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key that represents the column in this column collection.</param>
        /// <returns>
        /// The value of the database column with the corresponding <paramref name="key"/>.
        /// </returns>
        Int32 GetStat(StatType key);

        /// <summary>
        /// Gets the <paramref name="value"/> of the database column in the column collection `{0}`
        /// that corresponds to the given <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key that represents the column in this column collection.</param>
        /// <param name="value">The value to assign to the column with the corresponding <paramref name="key"/>.</param>
        void SetStat(StatType key, Int32 value);
    }
}