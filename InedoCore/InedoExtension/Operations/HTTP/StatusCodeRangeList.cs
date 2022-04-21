using System;
using System.Collections.Generic;
using System.Linq;

namespace Inedo.Extensions.Operations.HTTP
{
    internal sealed class StatusCodeRangeList
    {
        private List<Range> ranges;

        public static StatusCodeRangeList Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new StatusCodeRangeList { ranges = new List<Range>() };

            var values = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            return new StatusCodeRangeList { ranges = values.Select(Range.Parse).Where(r => r != null).ToList() };
        }

        private StatusCodeRangeList()
        {
        }

        public bool IsInAnyRange(int code)
        {
            return this.ranges.Any(r => r.IsInRange(code));
        }

        public override string ToString()
        {
            return string.Join(",", this.ranges.Select(r => r.ToString()));
        }

        private sealed class Range
        {
            private readonly int start;
            private readonly int end;

            private Range(int singleValue)
                : this(singleValue, singleValue)
            {
            }
            private Range(int start, int end)
            {
                if (start > end)
                    throw new ArgumentException("Invalid range.");

                this.start = start;
                this.end = end;
            }

            public static Range Parse(string value)
            {
                try
                {
                    if (value.IndexOfAny(new[] { ':', '-' }) < 0)
                        return new Range(int.Parse(value));

                    var split = value.Split(new[] { ':', '-' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    return new Range(int.Parse(split[0]), int.Parse(split[1]));
                }
                catch
                {
                }

                return null;
            }

            public bool IsInRange(int value) => this.start <= value && value <= this.end;
            public override string ToString()
            {
                if (this.start == this.end)
                    return this.start.ToString();
                else
                    return this.start.ToString() + ":" + this.end.ToString();
            }
        }
    }
}
