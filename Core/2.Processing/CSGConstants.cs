using System;

namespace Chisel.Core
{
    static partial class CSGConstants
    {
        const double        kFatPlaneWidthEpsilonDouble	    = 0.0006;

        public const float  kFatPlaneWidthEpsilon	    = (float)kFatPlaneWidthEpsilonDouble;

        public const double kDivideMinimumEpsilon       = 0.000001;
    }
}
