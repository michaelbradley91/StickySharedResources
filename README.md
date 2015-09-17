Sticky Shared Resources
=======================
Disclaimer
----------
This package is brand new and probably has bugs in it! If you find one, email michael.bradley@hotmail.co.uk and I'll look into it if I have time. Feel free to clone this repository and work on it yourself if you want.

I may also have re-invented the wheel - it was fun anyway!

Features That Might Be Coming
-----------------------------

* The ability to "Try" and acquire a set of resources (for a fixed number of milliseconds + giving up immediately).
* Some safety against threads being interrupted.

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

This example is designed to show that resource groups are powerful - they are able to do anything a binary semaphore can do.

### Connecting Resources
Suppose you have a remote control in code. Each button on the control is its own class, and interacts with a game to move a character up, down, left and right. For whatever reason, moving the character in two directions at once is not allowed, so you need to ensure that only one button's input is processed at any one time. How can you do this?

Firstly, we'll need each button to hold onto a resource. Suppose we define a button like this:

<pre>
<code>
public class Button
{
    private readonly SharedResource sharedResource;

    public Button(SharedResource sharedResource)
    {
        this.sharedResource = sharedResource;
    }

    public void Press()
    {
        var resourceGroup = SharedResourceGroup.CreateAcquiringSharedResources(sharedResource);
        // Do stuff...
        resourceGroup.FreeSharedResources();
    }
}
</code>
</pre>

When you press the button, it acquires its resource which we'll utilise to ensure no other button is being pressed.
The button then does its work, and once it is finished it will release its resource.

Now for the Remote Control. This will need to create its buttons and provide them with the resources. We can do this as follows:

<pre>
<code>
public class RemoteControl
{
    public readonly Button LeftButton;
    public readonly Button RightButton;
    public readonly Button UpButton;
    public readonly Button DownButton;

    // We're going to "Create - Connect - Allocate" the resources
    public RemoteControl()
    {
        // Create
        var resourceGroup = SharedResourceGroup.CreateWithNoAcquiredSharedResources();
        var leftButtonResource = resourceGroup.CreateAndAcquireSharedResource();
        var rightButtonResource = resourceGroup.CreateAndAcquireSharedResource();
        var upButtonResource = resourceGroup.CreateAndAcquireSharedResource();
        var downButtonResource = resourceGroup.CreateAndAcquireSharedResource();
        
        // Connect
        resourceGroup.ConnectSharedResources(leftButtonResource, rightButtonResource);
        resourceGroup.ConnectSharedResources(rightButtonResource, upButtonResource);
        resourceGroup.ConnectSharedResources(upButtonResource, downButtonResource);
        resourceGroup.FreeSharedResources();
        
        // Allocate
        LeftButton = new Button(leftButtonResource);
        RightButton = new Button(rightButtonResource);
        UpButton = new Button(upButtonResource);
        DownButton = new Button(downButtonResource);
    }
}
</code>
</pre>

The important new part is:

<code>resourceGroup.ConnectSharedResources(leftButtonResource, rightButtonResource)</code>

This connects those two resources. Resource groups obey this rule:

*To acquire a resource, you must acquire all resources that are connected to it, both directly and indirectly*

That's it! Now many threads can try to press each button at once, but only one button's press method will be able to do work at any one time.

There are a few important things to note:
* Each button is unaware of any other button. Even the resource passed to the button is unique to that button - everything else forgets it.
* A thread interacting with the buttons is unaware it is synchronising with every other button - it doesn't have to know that it needs to "lock" all the other buttons as well.
* When connecting resources, you don't have to connect each resource to every other resource. This will make a lot more sense if you know basic graph theory - connecting two resources adds an (undirected) edge between those resources. Two resources are connected if there is any path (something that follows the edges) in the graph between them. It may also help to think of connecting resource A to resource B as declaring that resource A depends on resource B.

Suppose later someone comes along with another remote control, and now it is possible to create a "super remote control" by combining the two. However, even on the super remote control you can still only press one button at a time. We can retrospectively combine the remote controls with a small adjustment:

<pre>
<code>
public class RemoteControl
{
    public readonly Button LeftButton;
    public readonly Button RightButton;
    public readonly Button UpButton;
    public readonly Button DownButton;
    
    private readonly SharedResource resource;

    public RemoteControl()
    {
        var resourceGroup = SharedResourceGroup.CreateWithNoAcquiredSharedResources();
        
        // Create a resource for the remote control itself. This gives it a handle on its buttons
        resource = resourceGroup.CreateAndAcquireSharedResource();
        var leftButtonResource = resourceGroup.CreateAndAcquireSharedResource();
        var rightButtonResource = resourceGroup.CreateAndAcquireSharedResource();
        var upButtonResource = resourceGroup.CreateAndAcquireSharedResource();
        var downButtonResource = resourceGroup.CreateAndAcquireSharedResource();
        
        // Connect the remote control's resource to all of the buttons.
        // (It is indirectly connected to the right, up and down buttons).
        resourceGroup.ConnectSharedResources(resource, leftButtonResource);
        resourceGroup.ConnectSharedResources(leftButtonResource, rightButtonResource);
        resourceGroup.ConnectSharedResources(rightButtonResource, upButtonResource);
        resourceGroup.ConnectSharedResources(upButtonResource, downButtonResource);
        resourceGroup.FreeSharedResources();

        LeftButton = new Button(leftButtonResource);
        RightButton = new Button(rightButtonResource);
        UpButton = new Button(upButtonResource);
        DownButton = new Button(downButtonResource);
    }

    // Combine the controls!
    public void CombineWithControl(RemoteControl otherRemoteControl)
    {
        var resourceGroup = SharedResourceGroup.CreateAcquiringSharedResources(resource, otherRemoteControl.resource);
        resourceGroup.ConnectSharedResources(resource, otherRemoteControl.resource);
        resourceGroup.FreeSharedResources();
    }
}
</code>
</pre>

Some highlights:
* The remote control now has a resource which, when acquired, prevents all of the buttons being pressed.
* When combining controls, the resource group is asked to acquire two **disconnected** resources. The resources can also be connected in this method (and you can pass as many as you like - params) but they don't have to be.
* The two controls are combined by connecting each remote control's resource. In terms of the graph of resources, this means that the buttons on both controls are now all connected. Hence, pressing a button on either control stops any button on the opposite control being pressed!

### Disconnecting Resources

Following from the example above, suppose the owner of the second control, Bob, wants to go home so Alice and Bob need to split the "super remote control" back into two boring regular controls. It would suck if Bob can only play his game in his home provided Alice is not playing her game in her home. We need to enable the controls to be used independently again.

We can do this with the following method:
<pre>
<code>
public void RemoveFromControl(RemoteControl otherRemoteControl)
{
    var resourceGroup = SharedResourceGroup.CreateAcquiringSharedResources(resource, otherRemoteControl.resource);
    resourceGroup.DisconnectSharedResources(resource, otherRemoteControl.resource);
    resourceGroup.FreeSharedResources();
}
</code>
</pre>

<sup>Note: while you could have stopped all buttons being pressed by acquiring either control's resource, resource groups are strict and require you to explicitly acquire any resources you operate on. This is to encourage your code to be aware of what is going on.</sup>

After the above method has been called, two threads can interact with the two controls at the same time, but only one button on each control can be pressed at any one time.

#### Warning About Disconnecting Resources

There's a long comment in code explaining this (on IResourceGroup). Coming back to graph theory, the disconnect method above only removes any edge it finds directly between the two resources it is given. This means that if the resources were connected in a cycle beforehand, they will remain connected even after the disconnect method has been called.

Possibly warranting some method renaming aside, this is considered a good thing. Your code has not shown enough awareness to be confident it "definitely" wants these resources to be disconnected. You can discover the graph of resources if you *have* to with the following:

<code>resourceGroup.GetSharedResourcesDirectlyConnectedTo(resource);</code>

The above returns all the immediate neighbours of the passed in resource. You can call this on said neighbours repeatedly to discover the structure of the entire graph, but if you need to you probably should try to refactor your code first.

To illustrate why this is a good thing, suppose three remote controls were connected together in a circle: Control A - Control B - Control C - Control A.
* If we disconnect control A from control B, should it be possible to use control A and control B at the same time even though control B is connected to C which is connected to A? Probably not.
* Would it be wise to automatically sever control C's connection from Control A as well? Again, probably not. The connection may have been added somewhere else completely in code, so you have no idea how important it is that these controls remain synchronised
* Would you be happy that this other area of code could also invalidate your code's belief the controls are synchronised? Hopefully not! That would be scary!!

Implementation Details
----------------------

### Efficiency
Each set of connected resources is locked by a single semaphore. This makes acquiring a large set of connected resources very cheap - O(1) ish ignoring other threads and flattening (below).

Connecting two resources is also essentially O(1). This is handled in a similar way to a disjoint set forest - the resources' parents are given a new shared parent (which is basically a semaphore). Whenever the parent is retrieved, the chain is immediately flattened to ensure at least decent amortised performance.

Disconnecting resources is more expensive. This can probably be improved, but currently disconnecting resource A from resource B takes time linear in the size of their original connected group. This is because their parent semaphores might need to be reset for each individual resource.

Acquiring disconnected resources in a resource group is done as follows:

1. Gather all the unique semaphores needs to acquire all the resources (at most one per resource).
2. Acquire each one in ascending order.
3. If after acquiring the resource, it is still the correct resource to acquire next, keep it and repeat on the next resource.

Step 3 can cause restarts. If the process restarts enough times, it closes something called a **gate**, which prevents further **new** resource groups from doing anything until this resource group has finished. This enforces a form of fairness.

### Memory
Resource groups are linear in the number of resources they were asked to acquire.

An individual resource can vary in memory, but if you have n resources the most memory they require is linear in n (ignoring big integers).

### Deadlock handling
A resource group avoids deadlock while acquiring disconnected resources by:

1. Always acquiring the resources in ascending order.
2. Relying on resources only ever increasing. Note that by increasing, resources can change the order in which they should be acquired, causing the acquisition process to partially restart. However, any resource already acquired can be kept.
