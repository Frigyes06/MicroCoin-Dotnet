﻿//-----------------------------------------------------------------------
// This file is part of MicroCoin - The first hungarian cryptocurrency
// Copyright (c) 2019 Peter Nemeth
// ServiceLocator.cs - Copyright (c) 2019 Németh Péter
//-----------------------------------------------------------------------
// MicroCoin is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MicroCoin is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU General Public License for more details.
//-----------------------------------------------------------------------
// You should have received a copy of the GNU General Public License
// along with MicroCoin. If not, see <http://www.gnu.org/licenses/>.
//-----------------------------------------------------------------------
using Microsoft.Extensions.DependencyInjection;
using Prism.Events;

namespace MicroCoin.Modularization
{
    public static class ServiceLocator
    {
        public static ServiceProvider ServiceProvider { get; set; }
        public static T GetService<T>() => ServiceProvider.GetService<T>();
        public static IEventAggregator EventAggregator => ServiceProvider.GetService<IEventAggregator>();
    }
}
