// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Most of the remaining warnings are related to test code and Moq usage patterns
// where null reference checks are handled by the testing framework itself
[assembly: SuppressMessage("Nullable", "CS8600:Converting null literal or possible null value to non-nullable type")]
[assembly: SuppressMessage("Nullable", "CS8602:Dereference of a possibly null reference")]
[assembly: SuppressMessage("Nullable", "CS8603:Possible null reference return")]
[assembly: SuppressMessage("Nullable", "CS8620:Argument cannot be used for parameter due to differences in the nullability of reference types")]
[assembly: SuppressMessage("Nullable", "CS8625:Cannot convert null literal to non-nullable reference type")]