using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpusMutatum {

	public static class GenExtensions {

		public static string ToNiceString<T>(this IEnumerable<T> l, string separator = ", ") {
			return "[" + string.Join(separator, l.Select(i => i.ToString()).ToArray()) + "]";
		}
	}
}
