namespace Khorde.Expr.Authoring
{
	[System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	public sealed class NodeCategoryAttribute : System.Attribute
	{
		readonly string category;

		public NodeCategoryAttribute(string category)
		{
			this.category = category;
		}

		public string Category
		{
			get { return category; }
		}
	}
}