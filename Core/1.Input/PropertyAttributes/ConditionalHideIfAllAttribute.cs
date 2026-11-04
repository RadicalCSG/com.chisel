namespace Chisel.Core
{
	// Hides a field when every one of the fields named has the value (ConditionalHide hides it when any of its conditions
	// does): a setting of the walls, say, when both sides have none
	public class ConditionalHideIfAllAttribute : ConditionalBaseAttribute
	{
		public readonly object   Value;
		public readonly string[] FieldsToCheck;

		public ConditionalHideIfAllAttribute(object value, params string[] fieldsToCheck)
		{
			this.Value         = value;
			this.FieldsToCheck = fieldsToCheck;
		}
	}
}
