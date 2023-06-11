// -----------------------------------------------------------------------
// <copyright file="TaskExtensions.cs" company="AillieoTech">
// Copyright (c) AillieoTech. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace AillieoUtils
{
    using System.Threading.Tasks;

    internal static class TaskExtensions
    {
        internal static async void Await(this Task task)
        {
            await task;
        }
    }
}
