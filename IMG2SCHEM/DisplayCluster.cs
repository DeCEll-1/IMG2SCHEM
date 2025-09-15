using MindustrySchematicCreator;

namespace IMG2SCHEM
{
    public class DisplayCluster
    {
        public List<DisplaySection> sections = new();
        public DisplayCluster Add(DisplaySection sec)
        {
            sections.Add(sec);
            return this;
        }
        public DisplayCluster CreateColorBlocks()
        {
            sections.ForEach(sec => sec.CreateColorBlocks());
            return this;
        }

        public DisplayCluster FillProcessorBlocks()
        {
            sections.ForEach(sec => sec.FillProcessorBlocks());
            return this;
        }

        public DisplayCluster FillSchem(Schematic schem)
        {
            sections.ForEach(sec => sec.FillSchem(schem));
            return this;
        }

    }
}
