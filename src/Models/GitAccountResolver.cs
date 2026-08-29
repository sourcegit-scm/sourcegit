using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public static class GitAccountResolver
    {
        public static GitAccount Resolve(
            IReadOnlyList<GitAccount> accounts,
            string configuredId,
            string userName,
            string email)
        {
            if (!string.IsNullOrWhiteSpace(configuredId))
            {
                foreach (var account in accounts)
                {
                    if (string.Equals(account.Id, configuredId, StringComparison.Ordinal))
                        return account;
                }
            }

            foreach (var account in accounts)
            {
                if (account.MatchesIdentity(userName, email))
                    return account;
            }

            return null;
        }
    }
}
