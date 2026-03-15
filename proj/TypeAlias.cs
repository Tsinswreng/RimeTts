#if false
<ItemGroup>
	<Compile Include="../TypeAlias.cs" />
</ItemGroup>
#endif

#pragma warning disable CS8981
global using u8 = System.Byte;
global using i8 = System.SByte;
global using u16 = System.UInt16;
global using i16 = System.Int16;
global using u32 = System.UInt32;
global using i32 = System.Int32;
global using u64 = System.UInt64;
global using i64 = System.Int64;
global using f32 = System.Single;
global using f64 = System.Double;
global using str = System.String;
global using obj = System.Object;
global using nil = System.Object;
global using CT = System.Threading.CancellationToken;

#pragma warning disable CS0436
global using static Tsinswreng.CsTypeAlias.Nil;
namespace Tsinswreng.CsTypeAlias {
	internal class Nil{
		public const nil NIL = null!;
	}
}


//使nil潙空object即可
//蔿防跨項目不一致、勿自定義public class Nil_{ public static Nil_ Nil = null!; }
//#pragma warning restore CS8981
