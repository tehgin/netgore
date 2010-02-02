﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using log4net.Core;

namespace DemoGame.Server
{
    /// <summary>
    /// Extension methods for the <see cref="Level"/> class.
    /// </summary>
    public static class LevelExtensions
    {
        /// <summary>
        /// Gets the brush to use for a log message.
        /// </summary>
        /// <param name="level">The log message level.</param>
        /// <returns>The brush to use for the <paramref name="level"/>.</returns>
        public static Brush GetColorBrush(this Level level)
        {
            if (level == Level.Debug)
                return Brushes.DarkGreen;

            if (level == Level.Info)
                return Brushes.DarkBlue;

            if (level == Level.Warn)
                return Brushes.DarkViolet;

            if (level == Level.Error)
                return Brushes.DarkRed;

            if (level == Level.Fatal)
                return Brushes.Red;

            return Brushes.Black;
        }

        /// <summary>
        /// Gets the color to use for a log message.
        /// </summary>
        /// <param name="level">The log message level.</param>
        /// <returns>The color to use for the <paramref name="level"/>.</returns>
        public static Color GetColor(this Level level)
        {
            if (level == Level.Debug)
                return Color.DarkGreen;

            if (level == Level.Info)
                return Color.DarkBlue;

            if (level == Level.Warn)
                return Color.DarkViolet;

            if (level == Level.Error)
                return Color.DarkRed;

            if (level == Level.Fatal)
                return Color.Red;

            return Color.Black;
        }
    }
}
