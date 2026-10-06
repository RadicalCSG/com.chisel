#define HAVE_SELF_CATEGORIES
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    [System.Diagnostics.DebuggerTypeProxy(typeof(CategoryRoutingRow.DebuggerProxy))]
    [StructLayout(LayoutKind.Explicit)]
    readonly unsafe struct CategoryRoutingRow
    {
        internal sealed class DebuggerProxy
        {
#if HAVE_SELF_CATEGORIES
			public int inside;
			public int aligned;
			public int selfAligned;
			public int selfReverseAligned;
			public int reverseAligned;
			public int outside;
			public DebuggerProxy(CategoryRoutingRow v)
			{
				inside = (int)v.inside;
				aligned = (int)v.aligned;
				selfAligned = (int)v.selfAligned;
				selfReverseAligned = (int)v.selfReverseAligned;
				reverseAligned = (int)v.reverseAligned;
				outside = (int)v.outside;
			}
#else
            public int inside;
            public int aligned;
            public int reverseAligned;
            public int outside;
            public DebuggerProxy(CategoryRoutingRow v)
            {
                inside          = (int)v.inside;
                aligned         = (int)v.aligned;
                reverseAligned  = (int)v.reverseAligned;
                outside         = (int)v.outside;
            }
#endif
		}

#if HAVE_SELF_CATEGORIES
		// Invalid is the sentinel, so it has to be a value no real destination can take. It used to be 255,
        // which stopped being safe the moment a node could have 255 rows.
        const ushort Invalid            = ushort.MaxValue;
        const ushort Inside             = (ushort)CategoryIndex.Inside;
        const ushort Aligned            = (ushort)CategoryIndex.Aligned;
        const ushort SelfAligned        = (ushort)CategoryIndex.SelfAligned;
        const ushort SelfReverseAligned = (ushort)CategoryIndex.SelfReverseAligned;
        const ushort ReverseAligned     = (ushort)CategoryIndex.ReverseAligned;
        const ushort Outside            = (ushort)CategoryIndex.Outside;
        
        public readonly static CategoryRoutingRow Identity              = new(Inside,  Aligned, SelfAligned, SelfReverseAligned, ReverseAligned, Outside);
        public readonly static CategoryRoutingRow AllInvalid            = new(Invalid, Invalid, Invalid, Invalid, Invalid, Invalid);
        public readonly static CategoryRoutingRow AllSelfAligned        = new(SelfAligned, SelfAligned, SelfAligned, SelfAligned, SelfAligned, SelfAligned);
        public readonly static CategoryRoutingRow AllSelfReverseAligned = new(SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned);
        public readonly static CategoryRoutingRow AllOutside            = new(Outside, Outside, Outside, Outside, Outside, Outside);
        public readonly static CategoryRoutingRow AllInside             = new(Inside,  Inside, Inside, Inside, Inside, Inside);

        public const int Length = (int)CategoryIndex.LastCategory + 1;

        // Is PolygonGroupIndex instead of int, but C# doesn't like that
        [FieldOffset(0)]  public readonly ushort inside;
        [FieldOffset(2)]  public readonly ushort aligned;
		[FieldOffset(4)]  public readonly ushort selfAligned;
		[FieldOffset(6)]  public readonly ushort selfReverseAligned;
		[FieldOffset(8)]  public readonly ushort reverseAligned;
        [FieldOffset(10)] public readonly ushort outside;
#else
		const ushort Invalid            = ushort.MaxValue;
        const ushort Inside             = (ushort)CategoryIndex.Inside;
        const ushort Aligned            = (ushort)CategoryIndex.Aligned;
        const ushort ReverseAligned     = (ushort)CategoryIndex.ReverseAligned;
        const ushort Outside            = (ushort)CategoryIndex.Outside;
        
        public readonly static CategoryRoutingRow Identity              = new(Inside,  Aligned, ReverseAligned, Outside);
        public readonly static CategoryRoutingRow AllInvalid            = new(Invalid, Invalid, Invalid, Invalid);
        public readonly static CategoryRoutingRow AllSelfAligned        = new(Aligned, Aligned, Aligned, Aligned);
        public readonly static CategoryRoutingRow AllSelfReverseAligned = new(ReverseAligned, ReverseAligned, ReverseAligned, ReverseAligned);
        public readonly static CategoryRoutingRow AllOutside            = new(Outside, Outside, Outside, Outside);
        public readonly static CategoryRoutingRow AllInside             = new(Inside,  Inside, Inside, Inside);

        public const int Length = (int)CategoryIndex.LastCategory + 1;

        // Is PolygonGroupIndex instead of int, but C# doesn't like that
        //[FieldOffset(0)] fixed byte destination[Length];
        [FieldOffset(0)] readonly ulong destination;
        [FieldOffset(0)] public readonly ushort inside;
        [FieldOffset(2)] public readonly ushort aligned;
        [FieldOffset(4)] public readonly ushort reverseAligned;
        [FieldOffset(6)] public readonly ushort outside;
#endif

		#region Operation tables           
#if HAVE_SELF_CATEGORIES
        public readonly static ushort[] kOperationTables = // NOTE: burst supports readonly static tables like this
        {
            // Additive set operation on polygons: output = (left-node || right-node)
            // Defines final output from combination of categorization of left and right node
            //new CategoryRoutingRow[] // Additive Operation
            //{
	        //  right node                                                                                                                 |
	        //                                            self                 self                                                        |
	        //  inside               aligned              aligned              reverse-aligned      reverse-aligned      outside           |    left-node       
	        //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                Inside,              Inside,              Inside,              Inside,              Inside,              Inside            , // inside
                Inside,              Aligned,             SelfAligned,         Inside,              Inside,              Aligned           , // aligned
                Inside,              Aligned,             SelfAligned,         Inside,              Inside,              SelfAligned       , // self-aligned
	            Inside,              Inside,              Inside,              SelfReverseAligned,  ReverseAligned,      SelfReverseAligned, // self-reverse-aligned
                Inside,              Inside,              Inside,              SelfReverseAligned,  ReverseAligned,      ReverseAligned    , // reverse-aligned
                Inside,              Aligned,             SelfAligned,         SelfReverseAligned,  ReverseAligned,      Outside           , // outside
            //},
                
            // Subtractive set operation on polygons: output = !(!left-node || right-node)
            // Defines final output from combination of categorization of left and right node
            //new CategoryRoutingRow[] // Subtractive Operation
            //{
	        //  right node                                                                                                                 |
	        //                                            self                 self                                                        |
	        //  inside               aligned              aligned              reverse-aligned      reverse-aligned      outside           |    left-node       
	        //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                Outside,             ReverseAligned,      SelfReverseAligned,  SelfAligned,         Aligned,             Inside            , // inside
                Outside,             Outside,             Outside,             Aligned,             Aligned,             Aligned           , // aligned
                Outside,             Outside,             Outside,             Aligned,             Aligned,             SelfAligned       , // self-aligned
                Outside,             ReverseAligned,      SelfReverseAligned,  Outside,             Outside,             SelfReverseAligned, // self-reverse-aligned
                Outside,             ReverseAligned,      SelfReverseAligned,  Outside,             Outside,             ReverseAligned    , // reverse-aligned
                Outside,             Outside,             Outside,             Outside,             Outside,             Outside           , // outside
            //},
                
            // Common set operation on polygons: output = !(!left-node || !right-node)
            // Defines final output from combination of categorization of left and right node
            //new CategoryRoutingRow[] // Intersection Operation
            //{
	        //  right node                                                                                                                 |
	        //                                            self                 self                                                        |
	        //  inside               aligned              aligned              reverse-aligned      reverse-aligned      outside           |    left-node       
	        //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                Inside,              Aligned,             SelfAligned,         SelfReverseAligned,  ReverseAligned,      Outside           , // inside
                Aligned,             Aligned,             SelfAligned,         Outside,             Outside,             Outside           , // aligned
                SelfAligned,         Aligned,             SelfAligned,         Outside,             Outside,             Outside           , // self-aligned
                SelfReverseAligned,  Outside,             Outside,             SelfReverseAligned,  ReverseAligned,      Outside           , // self-reverse-aligned
	            ReverseAligned,      Outside,             Outside,             SelfReverseAligned,  ReverseAligned,      Outside           , // reverse-aligned
                Outside,             Outside,             Outside,             Outside,             Outside,             Outside           , // outside
            //},
            
            // Additive set operation on polygons: output = (left-node || right-node)
            // Defines final output from combination of categorization of left and right node
            //new CategoryRoutingRow[] // AdditiveKeepInside Operation
            //{
	        //  right node                                                                                                                 |
	        //                                            self                 self                                                        |
	        //  inside               aligned              aligned              reverse-aligned      reverse-aligned      outside           |    left-node       
	        //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                Invalid,             Invalid,             Invalid,             Invalid,              Invalid,            Invalid           , // inside
                Invalid,             Invalid,             Invalid,             Invalid,              Invalid,            Invalid           , // aligned
                Invalid,             Invalid,             Invalid,             Invalid,              Invalid,            Invalid           , // self-aligned
	            Invalid,             Invalid,             Invalid,             Invalid,              Invalid,            Invalid           , // self-reverse-aligned
                Invalid,             Invalid,             Invalid,             Invalid,              Invalid,            Invalid           , // reverse-aligned
                Invalid,             Invalid,             Invalid,             Invalid,              Invalid,            Invalid           , // outside
            //}
        };
            
		public const int OperationCount  = 6;
        public const int RowStride       = OperationCount;
        public const int OperationStride = OperationCount * RowStride;
#else
		public readonly static ushort[] kOperationTables = // NOTE: burst supports readonly static tables like this
        {
            // Regular Operation Tables
            // Additive set operation on polygons: output = (left-node || right-node)
            // 
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
                //*
                Inside,              Inside,              Inside,              Inside               , // inside
                Inside,              Aligned,             Inside,              Aligned              , // aligned
                Inside,              Inside,              ReverseAligned,      ReverseAligned       , // reverse-aligned
                Inside,              Aligned,             ReverseAligned,      Outside              , // outside
                /*/
                Inside,              Inside,              Inside,              Inside               , // inside
                Inside,              Aligned,             Inside,              Aligned              , // aligned
                Inside,              Inside,              ReverseAligned,      ReverseAligned       , // reverse-aligned
                Inside,              Aligned,             ReverseAligned,      Outside              , // outside
                //*/
            //},

            // Subtractive set operation on polygons: output = !(!left-node || right-node)
            //
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
                Outside,             ReverseAligned,      Aligned,             Inside               , // inside
                Outside,             Outside,             Aligned,             Aligned              , // aligned
                Outside,             ReverseAligned,      Outside,             ReverseAligned       , // reverse-aligned
                Outside,             Outside,             Outside,             Outside              , // outside
            //},

            // Common set operation on polygons: output = !(!left-node || !right-node)
            //
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
                Inside,              Aligned,             ReverseAligned,      Outside              , // inside
                Aligned,             Aligned,             Outside,             Outside              , // aligned
	            ReverseAligned,      Outside,             ReverseAligned,      Outside              , // reverse-aligned
                Outside,             Outside,             Outside,             Outside              , // outside
            //},
            
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
	            Invalid,             Invalid,             Invalid,             Invalid              , // inside
                Invalid,             Invalid,             Invalid,             Invalid              , // aligned
                Invalid,             Invalid,             Invalid,             Invalid              , // reverse-aligned
                Invalid,             Invalid,             Invalid,             Invalid              , // outside
            //}
            



            // Remove Overlapping Tables
            // Additive set operation on polygons: output = (left-node || right-node)
            //
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
                /*
	            Outside,             Outside,             Outside,             Outside              , // inside
                Outside,             Outside,             Outside,             Outside              , // aligned
                Outside,             Outside,             Outside,             Outside              , // reverse-aligned
                Outside,             Outside,             Outside,             Outside              , // outside
                /*/
                Inside,              Inside,              Inside,              Inside               , // inside
                Inside,              Aligned,             Inside,              Aligned              , // aligned
                Inside,              Inside,              ReverseAligned,      ReverseAligned       , // reverse-aligned
                Inside,              Aligned,             ReverseAligned,      Outside              , // outside
                //*/
            //},

            // Subtractive set operation on polygons: output = !(!left-node || right-node)
            //
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
                Outside,             ReverseAligned,      Aligned,             Inside               , // inside
                Outside,             Outside,             Aligned,             Aligned              , // aligned
                Outside,             ReverseAligned,      Outside,             ReverseAligned       , // reverse-aligned
                Outside,             Outside,             Outside,             Outside              , // outside
            //}, 

            // Common set operation on polygons: output = !(!left-node || !right-node)
            //
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
                Inside,              Aligned,             ReverseAligned,      Outside              , // inside
                Aligned,             Aligned,             Outside,             Outside              , // aligned
                ReverseAligned,      Outside,             ReverseAligned,      Outside              , // reverse-aligned
                Outside,             Outside,             Outside,             Outside              , // outside
            //},
            
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
	            Invalid,             Invalid,             Invalid,             Invalid              , // inside
                Invalid,             Invalid,             Invalid,             Invalid              , // aligned
                Invalid,             Invalid,             Invalid,             Invalid              , // reverse-aligned
                Invalid,             Invalid,             Invalid,             Invalid              , // outside
            //}
            



            // Remove Overlapping Tables
            // Additive set operation on polygons: output = (left-node || right-node)
            //
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
                /*
	            Outside,             Outside,             Outside,             Outside              , // inside
                Outside,             Outside,             Outside,             Outside              , // aligned
                Outside,             Outside,             Outside,             Outside              , // reverse-aligned
                Outside,             Outside,             Outside,             Outside              , // outside
                /*/
                Inside,              Inside,              Inside,              Inside               , // inside
                Inside,              Inside,              Inside,              Aligned              , // aligned
                Inside,              Inside,              Inside,              ReverseAligned       , // reverse-aligned
                Inside,              Aligned,             ReverseAligned,      Outside              , // outside
                //*/
            //},

            // Subtractive set operation on polygons: output = !(!left-node || right-node)
            //
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
                Outside,             ReverseAligned,      Aligned,             Inside               , // inside
                Outside,             Outside,             Aligned,             Aligned              , // aligned
                Outside,             ReverseAligned,      Outside,             ReverseAligned       , // reverse-aligned
                Outside,             Outside,             Outside,             Outside              , // outside
            //}, 

            // Common set operation on polygons: output = !(!left-node || !right-node)
            //
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
                Inside,              Aligned,             ReverseAligned,      Outside              , // inside
                Aligned,             Aligned,             Outside,             Outside              , // aligned
                ReverseAligned,      Outside,             ReverseAligned,      Outside              , // reverse-aligned
                Outside,             Outside,             Outside,             Outside              , // outside
            //},
            
            //  right node                                                                          | Additive Operation
            //  inside               aligned              reverse-aligned      outside              |    left-node       
            //----------------------------------------------------------------------------------------------------------
	            Invalid,             Invalid,             Invalid,             Invalid              , // inside
                Invalid,             Invalid,             Invalid,             Invalid              , // aligned
                Invalid,             Invalid,             Invalid,             Invalid              , // reverse-aligned
                Invalid,             Invalid,             Invalid,             Invalid              , // outside
            //}
        };
		public const int OperationCount          = 4;
        public const int RowStride               = OperationCount;
		//public const int RemoveOverlappingOffset = OperationCount;
        public const int OperationStride         = OperationCount * RowStride;
#endif
		#endregion


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(int operationIndex, CategoryIndex left, in CategoryRoutingRow right)
		{
#if HAVE_SELF_CATEGORIES
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
			var operationOffset = operationIndex * OperationStride;

			// left = row, right = column
			var row = (byte)left * RowStride;
			//this.destination        = 0;
			this.inside             = kOperationTables[operationOffset + row + right.inside];
			this.aligned            = kOperationTables[operationOffset + row + right.aligned];
			this.selfAligned        = kOperationTables[operationOffset + row + right.selfAligned];
			this.selfReverseAligned = kOperationTables[operationOffset + row + right.selfReverseAligned];
			this.reverseAligned     = kOperationTables[operationOffset + row + right.reverseAligned];
			this.outside            = kOperationTables[operationOffset + row + right.outside];

#else
			// left = row, right = column
			var row = (byte)left;
			var operationOffset = operationIndex * OperationStride + (row * RowStride);
            this.destination    = 0;
            this.inside         = kOperationTables[(int)(operationOffset + (int)right.inside)];
            this.aligned        = kOperationTables[(int)(operationOffset + (int)right.aligned)];
            this.reverseAligned = kOperationTables[(int)(operationOffset + (int)right.reverseAligned)];
            this.outside        = kOperationTables[(int)(operationOffset + (int)right.outside)];
#endif
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CategoryRoutingRow operator +(CategoryRoutingRow oldRow, int offset)
        {
#if HAVE_SELF_CATEGORIES
            return new CategoryRoutingRow
            (
                inside             : (ushort)(oldRow.inside + offset),
                aligned            : (ushort)(oldRow.aligned + offset),
                selfAligned        : (ushort)(oldRow.selfAligned + offset),
                selfReverseAligned : (ushort)(oldRow.selfReverseAligned + offset),
                reverseAligned     : (ushort)(oldRow.reverseAligned + offset),
                outside            : (ushort)(oldRow.outside + offset)
            );
#else
            return new CategoryRoutingRow
            (
                inside          : (ushort)(oldRow.inside + offset),
                aligned         : (ushort)(oldRow.aligned + offset),
                reverseAligned  : (ushort)(oldRow.reverseAligned + offset),
                outside         : (ushort)(oldRow.outside + offset)
            );
#endif
		}

#if HAVE_SELF_CATEGORIES
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(ushort inside, ushort aligned, ushort selfAligned, ushort selfReverseAligned, ushort reverseAligned, ushort outside)
        {
            //this.destination        = 0;
            this.inside             = inside;
            this.aligned            = aligned;
			this.selfAligned        = selfAligned;
			this.selfReverseAligned = selfReverseAligned;
			this.reverseAligned     = reverseAligned;
            this.outside            = outside;
		}
#else
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(ushort inside, ushort aligned, ushort reverseAligned, ushort outside)
        {
            this.destination    = 0;
            this.inside         = inside;
            this.aligned        = aligned;
            this.reverseAligned = reverseAligned;
            this.outside        = outside;
		}
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(ushort value)
		{
#if HAVE_SELF_CATEGORIES
			//this.destination        = 0;
            this.inside             = value;
            this.aligned            = value;
			this.selfAligned        = value;
			this.selfReverseAligned = value;
			this.reverseAligned     = value;
            this.outside            = value;
#else
			this.destination    = 0;
            this.inside         = value;
            this.aligned        = value;
            this.reverseAligned = value;
            this.outside        = value;
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreAllTheSame()
		{
#if HAVE_SELF_CATEGORIES
			return inside == aligned &&
                   inside == selfAligned &&
				   inside == selfReverseAligned &&
                   inside == reverseAligned &&
				   inside == outside;
#else
			return inside == aligned &&
                   inside == reverseAligned &&
                   inside == outside;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreAllValue(int value)
		{
#if HAVE_SELF_CATEGORIES
			return (inside             == value &&
                    aligned            == value &&
					selfAligned        == value &&
					selfReverseAligned == value &&
					reverseAligned     == value &&
                    outside            == value);
#else
			return (inside          == value &&
                    aligned         == value &&
                    reverseAligned  == value &&
                    outside         == value);
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CategoryRoutingRow other)
        {
#if HAVE_SELF_CATEGORIES
            return (inside             == other.inside &&
                    aligned            == other.aligned &&
                    selfAligned        == other.selfAligned &&
                    selfReverseAligned == other.selfReverseAligned &&
                    reverseAligned     == other.reverseAligned &&
                    outside            == other.outside);
#else
            return (inside          == other.inside &&
                    aligned         == other.aligned &&
                    reverseAligned  == other.reverseAligned &&
                    outside         == other.outside);
#endif
		}

		public ushort this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                UnityEngine.Debug.Assert(index >= 0 && index < CategoryRoutingRow.Length);
#if HAVE_SELF_CATEGORIES
				switch(index)
                {
                    default:
                    case 0: return inside;
					case 1: return aligned;
					case 2: return selfAligned;
					case 3: return selfReverseAligned;
					case 4: return reverseAligned;
					case 5: return outside;
				}
#else
				return (ushort)((destination >> (index * 16)) & 0xFFFF);
#endif
			}
		}
    }
}