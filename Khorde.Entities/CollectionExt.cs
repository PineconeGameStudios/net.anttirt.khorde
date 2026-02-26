using Unity.Collections;

namespace Khorde.Entities
{
	public static class CollectionExt
	{
		/// <summary>
		/// Format hash as a <see cref="FixedString64Bytes"/> so it can be interpolated in burst code
		/// </summary>
		/// <param name="hash"></param>
		/// <returns></returns>
		public static FixedString64Bytes ToStringBurst(in this Unity.Entities.Hash128 hash)
		{
			FixedString32Bytes k_HexToLiteral = "0123456789abcdef";
			FixedString64Bytes chars = default;
			chars.Length = 32;

			for(int i = 0; i < 4; i++)
			{
				for(int j = 7; j >= 0; j--)
				{
					uint cur = hash.Value[i];
					cur >>= (j * 4);
					cur &= 0xF;
					chars[i * 8 + j] = k_HexToLiteral[(int)cur];
				}
			}

			return chars;
		}

	}
}