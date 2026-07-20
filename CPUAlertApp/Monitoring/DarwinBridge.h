#ifndef CPUALERT_DARWIN_BRIDGE_H
#define CPUALERT_DARWIN_BRIDGE_H

#include <stdbool.h>
#include <stdint.h>
#include <sys/types.h>

typedef struct {
    uint64_t user;
    uint64_t system;
    uint64_t idle;
    uint64_t nice;
} CPUASystemTicks;

typedef struct {
    pid_t pid;
    uint64_t start_time_ns;
    uint64_t cpu_time_ns;
    uint64_t physical_footprint_bytes;
    uint32_t uid;
    char name[256];
} CPUAProcessCounter;

typedef struct {
    uint64_t page_size;
    uint64_t active_pages;
    uint64_t wired_pages;
    uint64_t compressed_pages;
} CPUASystemMemoryStatistics;

typedef struct {
    uint64_t thread_id;
    uint64_t cpu_time_ns;
    char name[64];
} CPUAThreadCounter;

bool CPUACopySystemTicks(CPUASystemTicks *output);
bool CPUACopySystemMemoryStatistics(CPUASystemMemoryStatistics *output);
int CPUACopyAllPIDs(pid_t *buffer, int buffer_bytes);
bool CPUACopyProcessCounter(pid_t pid, CPUAProcessCounter *output);
int CPUACopyThreadIDs(pid_t pid, uint64_t *buffer, int buffer_bytes);
bool CPUACopyThreadCounter(pid_t pid, uint64_t thread_id, CPUAThreadCounter *output);

typedef enum {
    CPUA_GPU_SOURCE_UNAVAILABLE = 0,
    CPUA_GPU_SOURCE_IOREPORT = 1,
    CPUA_GPU_SOURCE_IOACCELERATOR = 2
} CPUAGPUSource;

typedef struct {
    double usage;
    CPUAGPUSource source;
} CPUAGPUSample;

typedef struct {
    uint64_t coalition_id;
    uint64_t gpu_time;
} CPUACoalitionGPUCounter;

void *CPUACreateGPUContext(void);
void CPUADestroyGPUContext(void *context);
bool CPUACopyGPUSample(void *context, CPUAGPUSample *output);
bool CPUACopyProcessCoalitionID(pid_t pid, uint64_t *output);
bool CPUACopyCoalitionGPUCounter(uint64_t coalition_id, CPUACoalitionGPUCounter *output);

#endif
