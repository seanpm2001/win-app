﻿/*
 * Copyright (c) 2022 Proton Technologies AG
 *
 * This file is part of ProtonVPN.
 *
 * ProtonVPN is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * ProtonVPN is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with ProtonVPN.  If not, see <https://www.gnu.org/licenses/>.
 */

using ProtonVPN.Api.Contracts;
using ProtonVPN.Api.Contracts.Servers;
using ProtonVPN.Core.Abstract;

namespace ProtonVPN.Core.Servers.Specs
{
    public class ExitCountryServer : Specification<LogicalServerResponse>
    {
        private readonly string _countryCode;

        public ExitCountryServer(string countryCode)
        {
            _countryCode = countryCode;
        }

        public override bool IsSatisfiedBy(LogicalServerResponse item)
        {
            return item.ExitCountry.Equals(_countryCode);
        }
    }
}