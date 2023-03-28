I think might be helpful to have an architecture and design doc separate from the actual code. Still check it in to git so we can see changes. Informal language is okay. Non-computerized docs like noteboook sketches are great too but not kept here and not as easy to examine history on.

# Dataflow

FileSystem notification => Acceptance Queue => Execution Queue => Executed

AcceptanceQueue => (applicationstate) => AcceptanceQueue.

On entering acceptance queue: copy files to a location where they will not be modified. This creates a game version.

Inside acceptance queue, user authorizes game versions in pairwise fashion, or enables auto-accept. While sending these to the execution queue, these files are copied again to new game directories along with ftherlnd files, so that they are ready to be executed as soon as they arrive in the execution queue. Execution queue has no affordances, it's just a visualizer.

Executed queue has a delete affordance and also a peek affordance to remember details.

## Design

In general, we want side effects to be the responsibility of the sender, although for file system we can't quite achieve that because it needs game information. But acceptanceQueue for example will take responsibility for keeping track of which new games have been authorized or created, and will send them to execution queue only when they are completely ready for Dominions5.exe. This means it needs more than just a queue. Internally it needs game state, and that means the dataflow is also circular: AcceptanceQueue => AcceptanceQueue.

## Implementation

Intermodule communications happens via side-effecting signals passed to the View. An alternative would be to allow update to pass a message back to other messages, but since Views are generally responsible for originating messages even to themselves, it's probably best to just let them use signals to dispatch to other modules too.

Hmmm. In fact, maybe the UI shouldn't be responsible for anything except affordances. If ExecutionQueue is just a visualized queue, maybe it shouldn't be in charge of originating its own messages at all. Maybe AcceptanceQueue or some other thing with internal logic should be responsible for all of that.

The thing is though, _affordances_ should be able to trigger logic. So maybe anything that depends on an affordance has to happen inside the MVU and not in Domain. Or at minimum it needs to be able to read from and trigger state changes in the global state object, whatever it is; and how is that different from being in the MVU?