using HuntersAccord.Core.Patches;

namespace HuntersAccord.Core.Tests;

public sealed class GamePatchesTests
{
    private const string Xml = """
        <Patch>
          <TitleID><ID>CUSA03173</ID></TitleID>
          <Metadata Title="Bloodborne" Name="Test Patch" Author="Someone" Note="n" PatchVer="1.0" AppVer="01.09" AppElf="eboot.bin">
            <PatchList>
              <Line Type="bytes" Address="0x00fbc40f" Value="eb1d"/>
              <Line Type="bytes32" Address="0x055289f8" Value="0x00000500"/>
              <Line Type="float32" Address="0x02434883" Value="0.5"/>
              <Line Type="utf16" Address="0x04cb39ea" Value="Hi"/>
            </PatchList>
          </Metadata>
          <Metadata Title="Bloodborne" Name="Old Version" Author="x" AppVer="01.00" AppElf="eboot.bin">
            <PatchList><Line Type="bytes" Address="0x00500000" Value="90"/></PatchList>
          </Metadata>
          <Metadata Title="Bloodborne" Name="Pattern" Author="x" AppVer="01.09" AppElf="eboot.bin">
            <PatchList><Line Type="mask" Address="48 8B ?? 90" Value="90" Offset="2"/></PatchList>
          </Metadata>
        </Patch>
        """;

    [Fact]
    public void Patches_for_the_version_become_eboot_writes()
    {
        var patches = GamePatches.Parse(Xml, "01.09");
        var patch = Assert.Single(patches); // other version and pattern-scan patches are left out
        Assert.Equal("Test Patch", patch.Name);
        Assert.Equal(4, patch.Lines.Count);
        Assert.Equal(0x00fbc40f - GamePatches.LoadBase, patch.Lines[0].Rva);
        Assert.Equal([0xEB, 0x1D], patch.Lines[0].Bytes);
        Assert.Equal([0x00, 0x05, 0x00, 0x00], patch.Lines[1].Bytes);
        Assert.Equal(BitConverter.GetBytes(0.5f), patch.Lines[2].Bytes);
        Assert.Equal([(byte)'H', 0, (byte)'i', 0, 0, 0], patch.Lines[3].Bytes);
    }
}
