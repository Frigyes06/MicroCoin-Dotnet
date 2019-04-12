﻿//-----------------------------------------------------------------------
// This file is part of MicroCoin - The first hungarian cryptocurrency
// Copyright (c) 2019 Peter Nemeth
// ICheckPointStorage.cs - Copyright (c) 2019 %UserDisplayName%
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
using MicroCoin.Chain;
using MicroCoin.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace MicroCoin.CheckPoints
{
    public interface ICheckPointStorage
    {
        void AddBlock(CheckPointBlock block);
        void AddBlocks(IEnumerable<CheckPointBlock> block);
        CheckPointBlock GetBlock(int blockNumber);
        Account GetAccount(AccountNumber accountNumber);
        void AddAccounts(IList<Account> modifiedAccounts);
    }
}
