using System;
using Mpr.Query;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Behavior.Authoring;

[Serializable]
[NodeCategory("Execution")]
internal class Query : ExecBase, IExecNode
{
    private INodeOption queryOption;

    public override void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context)
    {
        exec.type = BTExec.BTExecType.Wait;
        exec.data.wait = new Behavior.Wait
        {
            until = context.GetExpressionRef(GetInputPort(1)),
        };
    }

    protected override void OnDefineOptions(IOptionDefinitionContext context)
    {
        queryOption = context.AddOption<QueryGraphAsset>("Query")
            .WithDisplayName("Query")
            .Build();
    }

    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        context.AddInputPort<Exec>(EXEC_PORT_DEFAULT_NAME)
            .WithDisplayName(string.Empty)
            .WithConnectorUI(PortConnectorUI.Arrowhead)
            .WithPortCapacity(PortCapacity.Single)
            .Build();

        // so what does this do?
        // run the query
        // output an array of result items? where? a variable?
        // what's a variable?
        // we'll need to copy out of qsitemstorage to a behavior tree local variable
        // so we can keep using the results while another query runs
        // the only thing we can't do is run two queries on the same frame, but that's
        // ok, we can just have the BT/Query node queue things up
        // -> theoretically PendingQuery could also be a queue on top of a DynamicBuffer
        
        // this means we need variable storage per bt, including array variables
        // and an expression "read variable" node
        /*
         * - NOTE:
         * 
         *   during live baking in play mode, a component's blittable data in the
         *   live world gets overwritten IF the new bake output differs from the old bake
         *   output, but if baking results in the same value as before, then the volatile
         *   live value (which may differ from the initial baked value due to runtime changes)
         *   is not modified
         * 
         * [ ] variable storage / blackboard
         *   [X] storage representation design
         *     - scalars are constant size, and can fit in a buffer that doesn't need resizing
         *     - array storage
         *         A. fixed capacity
         *            - auto replication
         *            - auto deallocation
         *            - cannot go past max capacity
         *         B. allocation within dynamic buffer
         *            - auto replication
         *            - auto deallocation
         *            - custom allocator required
         *            - dynamic offsets in read/write nodes
         *         C. allocation outside
         *            - manual replication
         *            - manual deallocation
         *     - entities in replicated storage (whether scalar or array) need patching
         *        - store entities in a separate dynamic buffer with a [GhostField] Entity field
         *      [X] scalars
         *         - baked offsets (effectively a generated struct layout)
         *             - this breaks with hot reloads since the struct layout changes
         *                 - what's the solution here?
         *                    - for now just accept that all variables are zeroed on hot reload if the variables change
         *                    - can maybe be fixed later somehow
         *      [X] fixed-capacity arrays
         *         - baked offsets (effectively a generated struct layout)
         *    [ ] implementation
         *       - read all variables from the graph
         *       - subgraph storage is effectively a nested struct
         *          - each instance gets its own field?
         *              - maybe each subgraph can specify if it uses shared or per-instance variables
         *              - or maybe each *variable* can specify if it's per-subgraph-instance or global...
         *                  - this might not be supported by the graph toolkit though
         *              - for now, just a plain struct per instance
         *       - all variables get sorted by alignment and packed into field layouts
         *       - expression read & bt write nodes will have a simple offset for reading
         *       [ ] during expr baking, gather variables and compute layout into a reflection structure
         *       [ ] create a blackboard buffer type
         *       [ ] resolve read exprs from variables to reads from offsets using the reflection structure
         *       [ ] on bt graphs, resolve writes to variables
         *          - for BT/Query *specifically*, require target to be a variable directly,
         *            not another node of any other type
         *             - anything else would require some kind of arbitrary temporary storage
         *                 - this can be implemented later on the bt stack
         *                   (or as a separate parallel stack from the instruction pointer stack)
         *                     - at this point we're starting to approach a full UEBP implementation welp
         *                         - would be cool but it'd need a better implementation architecture to allow vectorized processing like  if that's the goal
         *          - add a separate BT node that just writes to a variable
         */
    }
}