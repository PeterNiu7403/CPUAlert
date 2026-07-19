#include <metal_stdlib>
using namespace metal;

kernel void stressKernel(
    device float *values [[buffer(0)]],
    uint index [[thread_position_in_grid]]
) {
    float value = values[index];
    for (uint iteration = 0; iteration < 4096; ++iteration) {
        value = fma(value, 1.000001f, 0.000001f);
    }
    values[index] = value;
}
