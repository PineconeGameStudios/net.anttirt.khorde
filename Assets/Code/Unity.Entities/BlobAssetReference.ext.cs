namespace Unity.Entities
{
    public static class BlobAssetReferenceExt
    {
        /// <summary>
        /// Reads bytes from a buffer, validates the expected serialized version, and deserializes them into a new blob asset.
        /// The returned blob reuses the data from the passed in pointer and is only valid as long as the buffer stays allocated.
        /// Also the returned blob asset reference can not be disposed.
        /// </summary>
        /// <param name="data">A pointer to the buffer containing the serialized blob data.</param>
        /// <param name="size">Size in bytes of the buffer containing the serialized blob data.</param>
        /// <param name="version">Expected version number of the blob data.</param>
        /// <param name="result">The resulting BlobAssetReference if the data was read successful.</param>
        /// <param name="numBytesRead">Number of bytes of the data buffer that are read.</param>
        /// <returns>A bool if the read was successful or not.</returns>
        public static unsafe bool TryReadInplace<T>(byte* data, long size, int version,
            out BlobAssetReference<T> result,
            out int numBytesRead)
            where T : unmanaged
        {
            return BlobAssetReference<T>.TryReadInplace(data, size, version, out result, out numBytesRead);
        }
        
    }
}