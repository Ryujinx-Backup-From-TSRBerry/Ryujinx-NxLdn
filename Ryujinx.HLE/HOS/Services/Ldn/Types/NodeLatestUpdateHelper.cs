namespace Ryujinx.HLE.HOS.Services.Ldn.Types
{
    internal static class NodeLatestUpdateHelper
    {
        public static void CalculateLatestUpdate(this NodeLatestUpdate[] array, NodeInfo[] beforeNodes, NodeInfo[] afterNodes)
        {
            if (beforeNodes == null)
            {
                return;
            }
            lock (array)
            {
                for (int i = 0; i < 8; i++)
                {
                    NodeInfo obj = ((beforeNodes == null) ? default(NodeInfo) : beforeNodes[i]);
                    NodeInfo after = ((afterNodes == null) ? default(NodeInfo) : afterNodes[i]);
                    if (obj.IsConnected == 0)
                    {
                        if (after.IsConnected != 0)
                        {
                            array[i].State |= NodeLatestUpdateFlags.Connect;
                        }
                    }
                    else if (after.IsConnected == 0)
                    {
                        array[i].State |= NodeLatestUpdateFlags.Disconnect;
                    }
                }
            }
        }

        public static NodeLatestUpdate[] ConsumeLatestUpdate(this NodeLatestUpdate[] array, int number)
        {
            NodeLatestUpdate[] result = new NodeLatestUpdate[number];
            lock (array)
            {
                for (int i = 0; i < number; i++)
                {
                    result[i].Reserved = new byte[7];
                    if (i < 8)
                    {
                        result[i].State = array[i].State;
                        array[i].State = NodeLatestUpdateFlags.None;
                    }
                }
                return result;
            }
        }
    }
}
