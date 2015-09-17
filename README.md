Sticky Shared Resources
=======================
Disclaimer
----------
This package is brand new and probably has bugs in it! If you find one, email michael.bradley@hotmail.co.uk and I'll look into it if I have time. Feel free to clone this repository and work on it yourself if you want.

Summary
-------
**Sticky Shared Resources** is a stepping stone towards simplifying concurrent programming.

It is going to be used in another project called "Pipes" which is significantly inspired by CSO, although will take a different approach to construction. You can find out about CSO [here](http://www.cs.ox.ac.uk/people/bernard.sufrin/CSO/cso-doc-scala2.11.4/index.html#ox.CSO$).

The typical use case for this package is when you want to create a collection of objects which must all (collectively) be accessed by at most one thread at a time, but you'd like the objects to be unaware of each other. In the terminology of the package, you handle this as follows:
* Call the objects you want to sychronize "Control Panels" (on a spaceship?)
* Create a Resource Group.
* Add to the Resource Group one Resource per Control Panel.
* Connect all of the Resources together.
* Dispose of the Resource Group (free its resources) and allocate them to the Control Panels.
* When Alice wants to a use a Control Panel, she is required to first acquire its Resource. With the package's help, once Alice succeeds you can be sure Bob is not interacting with **any** of the control panels - he's locked out until Alice is done.

The package lets you connect resources and disconnect resources at essentially "any time" provided you can first acquire them. It provides a method of acquiring sets of potentially disconnected resources which will do its best to avoid deadlock. (See implementation details at the end if you're interested). Essentially, if it is possible to avoid deadlock, it will.

The name **Sticky Shared Resources** comes from the following:
* "Shared Resources" comes from shared memory - memory that is accessed by multiple threads at once.
* "Sticky" comes from connecting the Resources together. If someone wants to acquire / check out a resource, they're forced to take all of the other resources stuck to it.

Code Examples
-------------
### Getting started
To do anything, you first need to create an empty resource group. Resource groups are the only way to create resources.

<code>var resourceGroup = SharedResourceGroup.CreateWithNoAcquiredSharedResources();</code>

A (shared) resource group holds a group / collection of resources which it has acquired. A resource group will only allow you to manipulate resources it has already acquired - you'll see that you can't modify the resources directly (much).

To create your first resource:

<code>var resource = resourceGroup.CreateAndAcquireSharedResource();</code>

This resource is automatically acquired by the resource group. If you try to acquire it with a different resource group, you'll be forced to wait until the resource group above has freed it.

A resource is just a "representation" of some actual object used by multiple threads - say a button on a remote control. When you push the button, it will first acquire its resource before processing your input. If you want to remember the object associated to a resource in a more complicated scenario, you can use:

<code>resource.AssociatedObject = "some other object"</code>

<sup>Note that this field is not synchronised in any way, so you should generally only set it once.

Once you're done with the resource/s in the resource group, you free them all at once as follows:

<code>resourceGroup.FreeSharedResources();</code>

What if you want to acquire the resource later, so your thread has access and you know that no other thread does? You simply do this:

<code>var resourceGroup = SharedResourceGroup.CreateAcquiringSharedResources(resource);</code>

With this knowledge, you can now create a semaphore! (In a very overcomplicated way)

#### Convoluted Semaphore Example

Here is a semaphore which initially starts "up" (Released) and can be pulled down once (WaitOne). Note that this semaphore is quite strict - if you try to bring it up when it is already up, it will throw an InvalidOperationException at you. Yuck!
<pre>
<code>
public class ConvolutedSemaphore
{
    private SharedResourceGroup activeResourceGroup;
    private readonly SharedResource resource;

    public ConvolutedSemaphore()
    {
        // Create a resource that will act as the internal semaphore
        var resourceGroup = SharedResourceGroup.CreateWithNoAcquiredSharedResources();
        resource = resourceGroup.CreateAndAcquireSharedResource();
        resourceGroup.FreeSharedResources();
    }

    public void Down()
    {
        // Once the group is created, it will have acquired the resource. Therefore, activeResourceGroup
        // won't be set until the resource is actually acquired and so it is already active.
        activeResourceGroup = SharedResourceGroup.CreateAcquiringSharedResources(resource);
    }

    public void Up()
    {
        // Allow other threads to acquire the resource (finish method down() above)
        activeResourceGroup.FreeSharedResources();
    }
}
</code>
</pre>

This example is designed to show that resource groups are powerful - they are able to do anything binary semaphores can.

### Connecting Resources
