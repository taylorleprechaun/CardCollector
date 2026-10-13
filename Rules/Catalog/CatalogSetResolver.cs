using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Repository;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Finds or creates the <see cref="CatalogSet"/> for a set code during one sync run. A set's name moves to the shortest
    /// name any printing gives it, since longer names tend to be promo variants of the base set's name.
    /// </summary>
    public sealed class CatalogSetResolver
    {
        private readonly CatalogSyncContext _context;
        private readonly CatalogDiffResult _result;
        private readonly Dictionary<string, CatalogSet> _setsByPrefix;
        private readonly CatalogState _state;

        public CatalogSetResolver(CatalogState state, CatalogSyncContext context, CatalogDiffResult result)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _result = result ?? throw new ArgumentNullException(nameof(result));
            _setsByPrefix = state.Sets.ToDictionary(s => s.Prefix, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>True when some catalog printing already uses a set code with this prefix.</summary>
        public bool IsKnownPrefix(string prefix) => _setsByPrefix.ContainsKey(prefix);

        /// <summary>Returns the set for the code's prefix, creating it on first sight and shortening its name when a shorter one appears.</summary>
        public CatalogSet Resolve(string setCode, string? setName)
        {
            var prefix = CardDataMapper.GetSetPrefix(setCode);
            if (_setsByPrefix.TryGetValue(prefix, out var set))
            {
                if (!string.IsNullOrWhiteSpace(setName) && setName.Length < set.Name.Length)
                {
                    set.Name = setName;
                    set.DateModified = _context.NowUTC;
                }

                set.TCGReleaseDate ??= _context.SetReleaseDateLookup(setCode);
                return set;
            }

            set = new CatalogSet
            {
                DateCreated = _context.NowUTC,
                DateModified = _context.NowUTC,
                Name = string.IsNullOrWhiteSpace(setName) ? prefix : setName,
                Prefix = prefix,
                TCGReleaseDate = _context.SetReleaseDateLookup(setCode)
            };
            _setsByPrefix[prefix] = set;
            _state.Sets.Add(set);
            _result.NewSets.Add(set);
            return set;
        }
    }
}
