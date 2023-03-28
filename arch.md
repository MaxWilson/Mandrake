I think might be helpful to have an architecture and design doc separate from the actual code. Still check it in to git so we can see changes. Informal language is okay. Non-computerized docs like noteboook sketches are great too but not kept here and not as easy to examine history on.

# Dataflow

FileSystem notification => Acceptance Queue => Execution Queue => Executed

On entering acceptance queue: copy files to a location where they will not be modified. This creates a game version.

Inside acceptance queue, user authorizes game versions in pairwise fashion, or enables auto-accept. While sending these to the execution queue, these files are copied again to new game directories along with ftherlnd files, so that they are ready to be executed as soon as they arrive in the execution queue. Execution queue has no affordances, it's just a visualizer.

Executed queue has a delete affordance and also a peek affordance to remember details.