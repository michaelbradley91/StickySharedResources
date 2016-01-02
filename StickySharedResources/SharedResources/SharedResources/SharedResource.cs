using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedResources.SharedResources
{
    /// <summary>
    /// The symbolic representation of a shared resource. This can be acquired and created by resource groups.
    /// 
    /// A shared resource is similar to a semaphore / lock in that you can acquire and free it. However, resource groups can connect
    /// shared resources to enforce that acquiring one resource acquires all resources it is connected to.
    /// </summary>
    public class SharedResource
    {
        private readonly SharedResourceIdentifier sharedResourceIdentifier;
        private readonly List<SharedResource> directlyConnectedSharedSharedResources;

        private IReadOnlyCollection<SharedResource> connectedSharedResourcesCache;
        private bool mustRecalculateConnectedSharedResources;

        /// <summary>
        /// Assign this to any object you wish to associate with this resource. This can be used to "remember" what acquiring this shared resource actually acquires.
        /// </summary>
        public object AssociatedObject { get; set; }

        internal SharedResource()
        {
            sharedResourceIdentifier = SharedResourceIdentifier.Create();
            directlyConnectedSharedSharedResources = new List<SharedResource> { this };
            mustRecalculateConnectedSharedResources = true;
            connectedSharedResourcesCache = GetConnectedSharedResources();
        }

        /// <summary>
        /// Sets the shared resource identifier to be used as the new root. This will cut any existing chain to the root shared resource identifier
        /// </summary>
        internal void ResetRootSharedResourceIdentifier(SharedResourceIdentifier newRootSharedResourceIdentifier)
        {
            sharedResourceIdentifier.SetParentSharedResourceIdentifier(newRootSharedResourceIdentifier);
        }

        internal SharedResourceIdentifier GetCurrentRootSharedResourceIdentifier()
        {
            return sharedResourceIdentifier.GetCurrentRootSharedResourceIdentifier();
        }

        internal void DirectlyConnect(SharedResource sharedResource)
        {
            if (!directlyConnectedSharedSharedResources.Contains(sharedResource))
            {
                directlyConnectedSharedSharedResources.Add(sharedResource);
                mustRecalculateConnectedSharedResources = sharedResource.mustRecalculateConnectedSharedResources = true;
            }
            if (!sharedResource.directlyConnectedSharedSharedResources.Contains(this))
            {
                sharedResource.directlyConnectedSharedSharedResources.Add(this);
                mustRecalculateConnectedSharedResources = sharedResource.mustRecalculateConnectedSharedResources = true;
            }
        }

        internal void RemoveDirectConnectionTo(SharedResource sharedResource)
        {
            if (sharedResource == this) throw new ArgumentException("You cannot disconnect a resource from itself.", "sharedResource");
            if (directlyConnectedSharedSharedResources.Contains(sharedResource))
            {
                directlyConnectedSharedSharedResources.Remove(sharedResource);
                mustRecalculateConnectedSharedResources = sharedResource.mustRecalculateConnectedSharedResources = true;
            }
            if (sharedResource.directlyConnectedSharedSharedResources.Contains(this))
            {
                sharedResource.directlyConnectedSharedSharedResources.Remove(this);
                mustRecalculateConnectedSharedResources = sharedResource.mustRecalculateConnectedSharedResources = true;
            }
        }

        internal IReadOnlyCollection<SharedResource> DirectlyConnectedSharedResources
        {
            get { return directlyConnectedSharedSharedResources; }
        }

        internal IReadOnlyCollection<SharedResource> ConnectedSharedResources
        {
            get
            {
                if (mustRecalculateConnectedSharedResources) connectedSharedResourcesCache = GetConnectedSharedResources();
                mustRecalculateConnectedSharedResources = false;
                return connectedSharedResourcesCache;
            }
        }

        private IReadOnlyCollection<SharedResource> GetConnectedSharedResources()
        {
            var allConnectedSharedResources = new HashSet<SharedResource>();
            var sharedResourcesToCheck = new Stack<SharedResource>();
            sharedResourcesToCheck.Push(this);
            while (sharedResourcesToCheck.Any())
            {
                var nextSharedResourceToCheck = sharedResourcesToCheck.Pop();
                foreach (var directlyConnectedResource in nextSharedResourceToCheck.DirectlyConnectedSharedResources)
                {
                    if (allConnectedSharedResources.Contains(directlyConnectedResource)) continue;

                    allConnectedSharedResources.Add(directlyConnectedResource);
                    sharedResourcesToCheck.Push(directlyConnectedResource);
                }
            }
            return allConnectedSharedResources.ToList();
        }

        /// <summary>
        /// Create a new shared resource and automatically connect it to this shared resource.
        /// This will acquire this resource during the operation, so you must not have acquired this already.
        /// This operation will free all its resources once it is finished.
        /// </summary>
        public SharedResource CreateAndConnect()
        {
            var resourceGroup = SharedResourceGroup.CreateAcquiringSharedResources(this);

            var childResource = resourceGroup.CreateAndAcquireSharedResource();
            resourceGroup.ConnectSharedResources(this, childResource);
            resourceGroup.FreeSharedResources();

            return childResource;
        }
        
        /// <summary>
        /// Create a new shared resource. The resource is not acquired after this method has returned.
        /// </summary>
        public static SharedResource Create()
        {
            var resourceGroup = SharedResourceGroup.CreateWithNoAcquiredSharedResources();
            var resource = resourceGroup.CreateAndAcquireSharedResource();
            resourceGroup.FreeSharedResources();
            return resource;
        }

        /// <summary>
        /// Create a new shared resource connected directly to each child resource passed in.
        /// This will aquire all child resources, so you must not have acquired these already.
        /// This operation will free all its resources once it is finished.
        /// </summary>
        public static SharedResource CreateAndConnect(params SharedResource[] childResources)
        {
            var resourceGroup = SharedResourceGroup.CreateAcquiringSharedResources(childResources);

            var parentResource = resourceGroup.CreateAndAcquireSharedResource();
            foreach (var childResource in childResources)
            {
                resourceGroup.ConnectSharedResources(parentResource, childResource);
            }
            resourceGroup.FreeSharedResources();

            return parentResource;
        }
    }
}
