using System;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Enums;
using SecretHistories.Fucine;

namespace Roost.Twins.Entities
{
    public class FucinePathPlus : FucinePath
    {
        private readonly string fullPath;
        public readonly string sphereMask;

        public readonly int maxSpheresToFind;
        public readonly SphereCategory[] acceptableCategories;
        public readonly string[] sphereId;

        public FucinePathPlus(string path, int maxSpheresToFind, SphereCategory[] acceptable = null, SphereCategory[] excluded = null) : base(path)
        {
            this.maxSpheresToFind = maxSpheresToFind;

            if (excluded == null && acceptable == null)
                acceptableCategories = defaultAcceptableCategories;
            else
            {
                acceptable = acceptable ?? allCategories;
                excluded = excluded ?? defaultExcludedCategories;
                acceptableCategories = acceptable.Except(excluded).ToArray();

                if (acceptableCategories.SequenceEqual(defaultAcceptableCategories))
                    acceptableCategories = defaultAcceptableCategories;
            }

            //guaranteeing that equivalent paths will have the same id for caching
            fullPath = $"{path}[{string.Join(",", this.acceptableCategories)}]+{maxSpheresToFind}";
            if (this.IsWild()) //removing asterisk so IndexOf() checks are correct
                sphereMask = path.Substring(1);
            else
                sphereMask = path;
        }

        public bool AcceptsCategory(SphereCategory sphereCategory)
        {
            return acceptableCategories.Contains(sphereCategory);
        }

        private static readonly SphereCategory[] allCategories = Enum.GetValues(typeof(SphereCategory)) as SphereCategory[];
        private static readonly SphereCategory[] defaultExcludedCategories = new SphereCategory[] { SphereCategory.Notes, SphereCategory.Null, SphereCategory.Meta };
        private static readonly SphereCategory[] defaultAcceptableCategories = allCategories.Except(defaultExcludedCategories).ToArray();
    }

}
