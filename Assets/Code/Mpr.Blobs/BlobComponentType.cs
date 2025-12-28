using Unity.Entities;

namespace Mpr.Blobs;

/// <summary>
/// Component type serializable in a blob asset
/// </summary>
public struct BlobComponentType
{
	public ulong stableTypeHash;
	public ComponentType.AccessMode accessModeType;

	public ComponentType ResolveComponentType()
	{
		var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(stableTypeHash);
		if(typeIndex == TypeIndex.Null)
			return default;

		var type = ComponentType.FromTypeIndex(typeIndex);
		type.AccessModeType = accessModeType;
		return type;
	}

	public BlobComponentType(ComponentType componentType)
	{
		stableTypeHash = TypeManager.GetTypeInfo(componentType.TypeIndex).StableTypeHash;
		accessModeType = componentType.AccessModeType;
	}

	public BlobComponentType(ulong stableTypeHash, ComponentType.AccessMode accessMode)
	{
		this.stableTypeHash = stableTypeHash;
		this.accessModeType = accessMode;
	}

	public static BlobComponentType Make<T>(ComponentType.AccessMode accessMode)
		=> new BlobComponentType(TypeManager.GetTypeInfo<T>().StableTypeHash, accessMode);
}