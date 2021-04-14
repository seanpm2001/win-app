﻿/*
 * Copyright (c) 2020 Proton Technologies AG
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

using System.Collections.Generic;
using System.Linq;
using ProtonVPN.Common.Extensions;
using ProtonVPN.Common.Helpers;
using ProtonVPN.Core.Abstract;
using ProtonVPN.Core.Api.Contracts;
using ProtonVPN.Core.Servers.Models;
using ProtonVPN.Core.Servers.Specs;
using ProtonVPN.Core.Settings;

namespace ProtonVPN.Core.Servers
{
    public class ServerManager
    {
        private readonly ServerNameComparer _serverNameComparer;
        private List<LogicalServerContract> _servers = new();
        private List<string> _countries = new();
        private readonly IUserStorage _userStorage;

        public ServerManager(IUserStorage userStorage)
        {
            _userStorage = userStorage;
            _serverNameComparer = new ServerNameComparer();
        }

        public ServerManager(IUserStorage userStorage, List<LogicalServerContract> servers) : this(userStorage)
        {
            _servers = servers;
        }

        public void Load(IReadOnlyCollection<LogicalServerContract> servers)
        {
            Ensure.NotEmpty(servers, nameof(servers));
            SaveServers(servers);
            SaveCountries(servers);
        }

        public virtual void UpdateLoads(IReadOnlyCollection<LogicalServerContract> servers)
        {
            Dictionary<string, LogicalServerContract> updatedServers = servers.ToDictionary(server => server.Id);
            foreach (LogicalServerContract server in _servers.Where(server => updatedServers.ContainsKey(server.Id)))
            {
                server.Load = updatedServers[server.Id].Load;
                server.Score = updatedServers[server.Id].Score;
            }
        }

        public IReadOnlyCollection<Server> GetServers(ISpecification<LogicalServerContract> spec)
        {
            sbyte userTier = _userStorage.User().MaxTier;

            return _servers
                .Where(spec.IsSatisfiedBy)
                .Select(Map)
                .OrderBy(s => s.Name.ContainsIgnoringCase("free") ? 0 : 1)
                .ThenBy(s => userTier < s.Tier)
                .ThenBy(s => s.Name, _serverNameComparer)
                .ToList();
        }

        public PhysicalServer GetPhysicalServerByServer(Server server)
        {
            return (from logical in _servers
                    from physical in logical.Servers
                    where logical.Id == server.Id && physical.ExitIp == server.ExitIp
                    select Map(physical))
                .FirstOrDefault();
        }

        public virtual Server GetServer(ISpecification<LogicalServerContract> spec)
        {
            return Map(_servers.Find(spec.IsSatisfiedBy));
        }

        public void MarkServerUnderMaintenance(string exitIp)
        {
            foreach (PhysicalServerContract server in _servers.SelectMany(logical =>
                logical.Servers.Where(server => server.ExitIp == exitIp)))
            {
                server.Status = 0;
            }
        }

        public virtual IReadOnlyCollection<string> GetCountries()
        {
            return _countries;
        }

        public virtual IReadOnlyCollection<string> GetCountriesWithFreeServers()
        {
            List<string> result = new();
            foreach (LogicalServerContract server in _servers)
            {
                if (server.Tier.Equals(ServerTiers.Free) && !result.Contains(server.EntryCountry))
                {
                    result.Add(server.EntryCountry);
                }
            }

            return result;
        }

        public bool CountryHasAvailableServers(string country, sbyte userTier)
        {
            IReadOnlyCollection<Server> servers = GetServers(new EntryCountryServer(country) && !new TorServer());
            return servers.FirstOrDefault(s => userTier >= s.Tier) != null;
        }

        public bool CountryHasAvailableSecureCoreServers(string country, sbyte userTier)
        {
            IReadOnlyCollection<Server> servers = GetServers(new SecureCoreServer() && new ExitCountryServer(country));
            return servers.FirstOrDefault(s => userTier >= s.Tier) != null;
        }

        public bool CountryUnderMaintenance(string country)
        {
            IReadOnlyCollection<Server> servers = GetServers(new OnlineServer() && new ExitCountryServer(country));
            return servers.Count == 0;
        }

        public bool Empty() => !_servers.Any();

        private void SaveServers(IEnumerable<LogicalServerContract> servers)
        {
            _servers = servers.Where(s => s != null).ToList();
        }

        private void SaveCountries(IEnumerable<LogicalServerContract> servers)
        {
            List<string> countryCodes = new();
            foreach (LogicalServerContract server in servers)
            {
                if (server == null)
                {
                    continue;
                }

                if (!IsCountry(server))
                {
                    continue;
                }

                if (countryCodes.Contains(server.EntryCountry))
                {
                    continue;
                }

                countryCodes.Add(server.EntryCountry);
            }

            _countries = countryCodes;
        }

        private static bool IsCountry(LogicalServerContract server)
        {
            string code = server.EntryCountry;
            if (code.Equals("AA") || code.Equals("ZZ") || code.StartsWith("X"))
            {
                return false;
            }

            string[] letters = {"M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"};
            if (code.StartsWith("Q") && letters.Contains(code.Substring(1, 1)))
            {
                return false;
            }

            return true;
        }

        private static Server Map(LogicalServerContract item)
        {
            if (item == null)
            {
                return null;
            }

            List<PhysicalServer> physicalServers = item.Servers.Select(Map).ToList();

            return new Server(
                item.Id,
                item.Name,
                item.City,
                item.EntryCountry,
                item.ExitCountry,
                item.Domain,
                item.Status,
                item.Tier,
                item.Features,
                item.Load,
                item.Score,
                item.Location,
                physicalServers,
                ExitIp(physicalServers)
            );
        }

        private static PhysicalServer Map(PhysicalServerContract server)
        {
            return new PhysicalServer(
                id: server.Id,
                entryIp: server.EntryIp,
                exitIp: server.ExitIp,
                domain: server.Domain,
                label: server.Label,
                status: server.Status);
        }

        /// <summary>
        /// If ExitIp is same on all physical servers, it is returned.
        /// </summary>
        private static string ExitIp(IEnumerable<PhysicalServer> servers)
        {
            return servers.Aggregate(
                (string)null,
                (ip, p) => ip == null || ip == p.ExitIp ? p.ExitIp : "",
                ip => !string.IsNullOrEmpty(ip) ? ip : null);
        }
    }
}