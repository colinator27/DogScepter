using System;

namespace DogScepterLib.Project.Util;

// BitArray class yielding higher performance, primarily for collision masks
public class FastBitArray
{
    public int[] Array;
    public int Length;

    public FastBitArray(int length)
    {
        Length = length;
        int arrayLength = ((length - 1) / 32) + 1;
        Array = new int[arrayLength];
    }

    public unsafe FastBitArray(byte[] data)
    {
        Length = data.Length * 8;
        int arrayLength = ((Length - 1) / 32) + 1;
        Array = new int[arrayLength];

        if (data.Length == 0)
            return;

        fixed (int* intPtr = &Array[0])
        {
            byte* pos1 = (byte*)intPtr;

            fixed (byte* ptr = &data[0])
            {
                byte* pos2 = ptr;

                for (int i = 0; i < data.Length; i++)
                {
                    *(pos1++) = *(pos2++);
                }
            }
        }
    }

    public unsafe FastBitArray(ReadOnlySpan<byte> data)
    {
        Length = data.Length * 8;
        int arrayLength = ((Length - 1) / 32) + 1;
        Array = new int[arrayLength];

        if (data.Length == 0)
            return;

        fixed (int* intPtr = &Array[0])
        {
            byte* pos1 = (byte*)intPtr;

            fixed (byte* ptr = &data[0])
            {
                byte* pos2 = ptr;

                for (int i = 0; i < data.Length; i++)
                {
                    *(pos1++) = *(pos2++);
                }
            }
        }
    }

    public unsafe byte[] ToByteArray()
    {
        int len = ((Length + 7) & (-8)) / 8;
        if (Length == 0 || len == 0)
            return System.Array.Empty<byte>();

        byte[] res = new byte[len];

        fixed (byte* ptr = &res[0])
        {
            byte* pos1 = ptr;

            fixed (int* intPtr = &Array[0])
            {
                byte* pos2 = (byte*)intPtr;

                for (int i = 0; i < len; i++)
                {
                    *(pos1++) = *(pos2++);
                }
            }
        }

        return res;
    }

    public void SetAllTrue()
    {
        unsafe
        {
            fixed (int* ptr = &Array[0])
            {
                uint* currPtr = (uint*)ptr;
                int len = Array.Length;
                for (int i = 0; i < len; i++)
                    *(currPtr++) = 0xFFFFFFFF;
            }
        }
    }

    public bool Get(int ind)
    {
        return (Array[ind / 32] & (1 << (ind & 31))) != 0;
    }

    public void SetTrue(int ind)
    {
        Array[ind / 32] |= (1 << (ind & 31));
    }

    // Reverse bit order
    public bool GetReverse(int ind)
    {
        int pos = ind & 31;
        return (Array[ind / 32] & (1 << (7 - (pos & 7) + (pos & (-8))))) != 0;
    }

    public void SetTrueReverse(int ind)
    {
        int pos = ind & 31;
        Array[ind / 32] |= (1 << (7 - (pos & 7) + (pos & (-8))));
    }

    public unsafe bool And(FastBitArray other, int setIndex)
    {
        bool changed = false;

        int setIndexArr = setIndex / 32;
        fixed (int* ptr = &Array[0])
        {
            fixed (int* ptr2 = &other.Array[0])
            {
                int* currPtr = ptr;
                int* otherPtr = ptr2;

                int i;
                int len = Array.Length;
                for (i = 0; i < len; i++)
                {
                    int before = *currPtr;
                    int after = before;
                    after &= *otherPtr;
                    if (setIndexArr == i)
                        after |= (1 << (setIndex & 31));
                    if (before != after)
                    {
                        *currPtr = after;
                        currPtr++;
                        otherPtr++;
                        changed = true;
                        break;
                    }
                    currPtr++;
                    otherPtr++;
                }
                for (i++; i < len; i++)
                {
                    *currPtr &= *otherPtr;
                    if (setIndexArr == i)
                        *currPtr |= (1 << (setIndex & 31));
                    currPtr++;
                    otherPtr++;
                }
            }
        }

        return changed;
    }
}
