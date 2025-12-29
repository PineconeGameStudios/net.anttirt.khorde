using Unity.Entities;

namespace Mpr.Expr.Authoring;

public abstract class CustomExprNodeBase<T, U> where T : CustomExprNodeBase<T, U> where U : unmanaged, ICustomExpr
{
    public /*override*/ void Bake(ref BlobBuilder builder, ref BTExpr expr, ExprBakingContext context)
    {
        expr.type = BTExpr.BTExprType.CustomExpr;
        expr.data.customExpr = new BTExpr.CustomExpr
        {
            stableTypeHash = TypeManager.GetTypeInfo<U>().StableTypeHash,
        };

        unsafe
        {
            fixed (BlobPtr<byte>* pdataPtr = &expr.data.customExpr.dataPtr)
            {
                ref BlobPtr<U> dataPtr = ref *(BlobPtr<U>*)pdataPtr;
                ref U result = ref builder.Allocate(ref dataPtr);
                Bake(ref builder, ref result, context);
            }
        }
    }

    public abstract void Bake(ref BlobBuilder builder, ref U data, ExprBakingContext context);
}

