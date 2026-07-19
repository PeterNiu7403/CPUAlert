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
    uint32_t uid;
    char name[256];
} CPUAProcessCounter;

typedef struct {
    uint64_t thread_id;
    uint64_t cpu_time_ns;
    char name[64];
} CPUAThreadCounter;

bool CPUACopySystemTicks(CPUASystemTicks *output);
int CPUACopyAllPIDs(pid_t *buffer, int buffer_bytes);
bool CPUACopyProcessCounter(pid_t pid, CPUAProcessCounter *output);
int CPUACopyThreadIDs(pid_t pid, uint64_t *buffer, int buffer_bytes);
bool CPUACopyThreadCounter(pid_t pid, uint64_t thread_id, CPUAThreadCounter *output);

#endif
