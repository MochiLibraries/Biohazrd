using ClangSharp.Interop;
using System;
using System.Runtime.InteropServices;

namespace ClangSharpTest2020
{
    internal static class PathogenExtensions
    {
        private struct PathogenTypeSizes
        {
            public int SizeOfPathogenTypeSizes;
            public int PathogenRecordLayout;
            public int PathogenRecordField;
            public int PathogenVTable;
            public int PathogenVTableEntry;
        }

        [DllImport("libclang.dll", ExactSpelling = true)]
        private static extern byte pathogen_GetTypeSizes(ref PathogenTypeSizes sizes);

        unsafe static PathogenExtensions()
        {
            // Validate the size of types between C# and C++ as a sanity check
            PathogenTypeSizes sizes = new PathogenTypeSizes()
            {
                SizeOfPathogenTypeSizes = sizeof(PathogenTypeSizes)
            };

            if (pathogen_GetTypeSizes(ref sizes) == 0)
            { throw new InvalidOperationException($"Cannot initialize Pathogen libclang extensions, sizeof({nameof(PathogenTypeSizes)} is wrong."); }

            if (sizes.PathogenRecordLayout != sizeof(PathogenRecordLayout))
            { throw new InvalidOperationException($"Cannot initialize Pathogen libclang extensions, sizeof({nameof(PathogenRecordLayout)} is wrong."); }

            if (sizes.PathogenRecordField != sizeof(PathogenRecordField))
            { throw new InvalidOperationException($"Cannot initialize Pathogen libclang extensions, sizeof({nameof(PathogenRecordField)} is wrong."); }

            if (sizes.PathogenVTable != sizeof(PathogenVTable))
            { throw new InvalidOperationException($"Cannot initialize Pathogen libclang extensions, sizeof({nameof(PathogenVTable)} is wrong."); }

            if (sizes.PathogenVTableEntry != sizeof(PathogenVTableEntry))
            { throw new InvalidOperationException($"Cannot initialize Pathogen libclang extensions, sizeof({nameof(PathogenVTableEntry)} is wrong."); }
        }

        [DllImport("libclang.dll", ExactSpelling = true)]
        public static extern unsafe PathogenRecordLayout* pathogen_GetRecordLayout(CXCursor cursor);

        [DllImport("libclang.dll", ExactSpelling = true)]
        public static extern unsafe void pathogen_DeleteRecordLayout(PathogenRecordLayout* layout);

        [DllImport("libclang.dll", ExactSpelling = true)]
        public static extern int pathogen_Location_isFromMainFile(CXSourceLocation location);
    }

    internal enum PathogenRecordFieldKind : int
    {
        Normal,
        VTablePtr,
        NonVirtualBase,
        VirtualBaseTablePtr, //!< Only appears in Microsoft ABI
        VTorDisp, //!< Only appears in Microsoft ABI
        VirtualBase,
    }

    internal enum PathogenVTableEntryKind : int
    {
        VCallOffset,
        VBaseOffset,
        OffsetToTop,
        RTTI,
        FunctionPointer,
        CompleteDestructorPointer,
        DeletingDestructorPointer,
        UnusedFunctionPointer,
    }

    internal static class PathogenVTableEntryKindEx
    {
        public static bool IsFunctionPointerKind(this PathogenVTableEntryKind kind)
        {
            switch (kind)
            {
                case PathogenVTableEntryKind.FunctionPointer:
                case PathogenVTableEntryKind.CompleteDestructorPointer:
                case PathogenVTableEntryKind.DeletingDestructorPointer:
                case PathogenVTableEntryKind.UnusedFunctionPointer:
                    return true;
                default:
                    return false;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PathogenVTableEntry
    {
        public PathogenVTableEntryKind Kind;

        //! Only relevant when Kind == FunctionPointer, CompleteDestructorPointer, DeletingDestructorPointer, or UnusedFunctionPointer
        public CXCursor MethodDeclaration;

        //! Only relevant when Kind == RTTI
        public CXCursor RttiType;

        //! Only relevant when Kind == VCallOffset, VBaseOffset, or OffsetToTop
        public long Offset;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PathogenVTable
    {
        public int EntryCount;
        public PathogenVTableEntry* Entries;

        public PathogenVTable* NextVTable;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PathogenRecordField
    {
        public PathogenRecordFieldKind Kind;
        public long Offset;
        public PathogenRecordField* NextField;
        public CXString Name;

        //! When Kind == Normal, this is the type of the field
        //! When Kind == NonVirtualBase, VTorDisp, or VirtualBase, this is the type of the base
        //! When Kind == VTablePtr or VirtualBaseTablePtr, this is void*
        public CXType Type;

        // Only relevant when Kind == fieldKind_Normal
        public CXCursor FieldDeclaration;
        public byte IsBitField;

        // Only relevant when IsBitField == true
        public uint BitFieldStart;
        public uint BitFieldWidth;

        // Only relevant when Kind == PathogenRecordFieldKind::NonVirtualBase or PathogenRecordFieldKind::VirtialBase
        public byte IsPrimaryBase;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PathogenRecordLayout
    {
        public PathogenRecordField* FirstField;
        public PathogenVTable* FirstVTable;

        public long Size;
        public long Alignment;

        // For C++ records only
        public byte IsCppRecord;
        public long NonVirtualSize;
        public long NonVirtualAlignment;
    }
}
