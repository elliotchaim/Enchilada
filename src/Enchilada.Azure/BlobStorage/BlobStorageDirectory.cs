﻿namespace Enchilada.Azure.BlobStorage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Infrastructure.Interface;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class BlobStorageDirectory : IDirectory
    {
        private readonly CloudBlobContainer BlobContainer;
        private readonly string Path;
        protected DirectoryInfo BackingDirectory;

        public string Name => BlobContainer.GetDirectoryReference( Path ).Prefix;
        public DateTime? LastModified => GetBlobFiles().OrderBy( x => x.LastModified ).FirstOrDefault()?.LastModified;

        public bool IsDirectory => true;

        public string RealPath => BlobContainer.GetDirectoryReference( Path ).Uri.ToString().StripDoubleSlash();

        public IEnumerable<IFile> Files => new ReadOnlyCollection<IFile>( GetBlobFiles().ToList() );

        public IEnumerable<IDirectory> Directories => new ReadOnlyCollection<IDirectory>( GetBlobDirectories().ToList() );

        public bool Exists => true;

        public BlobStorageDirectory( CloudBlobContainer blobContainer, string path )
        {
            BlobContainer = blobContainer;
            Path = path;
        }

        public async Task DeleteAsync( bool recursive = true )
        {
            BlobContinuationToken continuationToken = null;
            do
            {
                var response = Task.Run( () => BlobContainer.ListBlobsSegmentedAsync( Path, true, new BlobListingDetails(), 999, continuationToken, new BlobRequestOptions(), null ) ).Result;
                continuationToken = response.ContinuationToken;

                foreach ( var result in response.Results )
                {
                    var blob = result as CloudBlob;
                    if ( blob != null )
                    {
                        await blob.DeleteIfExistsAsync();
                    }
                }
            } while ( continuationToken != null );
        }

        public IEnumerator<INode> GetEnumerator()
        {
            return Directories
                .Union( Files.Cast<INode>() )
                .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IReadOnlyCollection<IDirectory> GetDirectories( string path )
        {
            return new ReadOnlyCollection<IDirectory>( GetBlobDirectories( path ).ToList() );
        }

        public IDirectory GetDirectory( string path )
        {
            return new BlobStorageDirectory( BlobContainer, path );
        }

        public IFile GetFile( string fileName )
        {
            return new BlobStorageFile( BlobContainer, fileName );
        }

        public void CreateDirectory() {}

        private IEnumerable<IDirectory> GetBlobDirectories( string path = null )
        {
            BlobContinuationToken continuationToken = null;
            do
            {
                var response = Task.Run( () => BlobContainer.ListBlobsSegmentedAsync( Path, continuationToken ) ).Result;
                continuationToken = response.ContinuationToken;

                foreach ( var result in response.Results.Where( x => x is CloudBlobDirectory && path == null || ( (CloudBlobDirectory) x ).Prefix.EndsWith( $"{path}/" ) ) )
                {
                    yield return new BlobStorageDirectory( BlobContainer, ( (CloudBlobDirectory) result ).Prefix );
                }
            } while ( continuationToken != null );
        }

        private IEnumerable<IFile> GetBlobFiles()
        {
            BlobContinuationToken continuationToken = null;
            do
            {
                var response = Task.Run( () => BlobContainer.GetDirectoryReference( Path ).ListBlobsSegmentedAsync( continuationToken ) ).Result;
                continuationToken = response.ContinuationToken;

                foreach ( var result in response.Results.Where( x => x is CloudBlockBlob ) )
                {
                    yield return new BlobStorageFile( BlobContainer, ( (CloudBlockBlob) result ).Uri.ToString() );
                }
            } while ( continuationToken != null );
        }
    }
}