#include "DarwinBridge.h"

#include <libproc.h>
#include <mach/mach.h>
#include <mach/mach_host.h>
#include <string.h>
#include <sys/proc_info.h>
#include <sys/resource.h>

bool CPUACopySystemTicks(CPUASystemTicks *output) {
    if (output == NULL) return false;
    host_cpu_load_info_data_t info = {0};
    mach_msg_type_number_t count = HOST_CPU_LOAD_INFO_COUNT;
    kern_return_t result = host_statistics(
        mach_host_self(), HOST_CPU_LOAD_INFO,
        (host_info_t)&info, &count
    );
    if (result != KERN_SUCCESS) return false;
    output->user = info.cpu_ticks[CPU_STATE_USER];
    output->system = info.cpu_ticks[CPU_STATE_SYSTEM];
    output->idle = info.cpu_ticks[CPU_STATE_IDLE];
    output->nice = info.cpu_ticks[CPU_STATE_NICE];
    return true;
}

int CPUACopyAllPIDs(pid_t *buffer, int buffer_bytes) {
    return proc_listpids(PROC_ALL_PIDS, 0, buffer, buffer_bytes);
}

bool CPUACopyProcessCounter(pid_t pid, CPUAProcessCounter *output) {
    if (output == NULL || pid <= 0) return false;
    struct proc_bsdinfo bsd = {0};
    if (proc_pidinfo(pid, PROC_PIDTBSDINFO, 0, &bsd, sizeof(bsd)) != sizeof(bsd)) {
        return false;
    }
    rusage_info_current usage = {0};
    if (proc_pid_rusage(pid, RUSAGE_INFO_CURRENT, (rusage_info_t *)&usage) != 0) {
        return false;
    }
    memset(output, 0, sizeof(*output));
    output->pid = pid;
    output->start_time_ns = (uint64_t)bsd.pbi_start_tvsec * 1000000000ULL
        + (uint64_t)bsd.pbi_start_tvusec * 1000ULL;
    output->cpu_time_ns = usage.ri_user_time + usage.ri_system_time;
    output->uid = bsd.pbi_uid;
    strlcpy(output->name, bsd.pbi_name, sizeof(output->name));
    return true;
}

int CPUACopyThreadIDs(pid_t pid, uint64_t *buffer, int buffer_bytes) {
    return proc_pidinfo(pid, PROC_PIDLISTTHREADS, 0, buffer, buffer_bytes);
}

bool CPUACopyThreadCounter(pid_t pid, uint64_t thread_id, CPUAThreadCounter *output) {
    if (output == NULL || pid <= 0 || thread_id == 0) return false;
    struct proc_threadinfo info = {0};
    int size = proc_pidinfo(pid, PROC_PIDTHREADINFO, thread_id, &info, sizeof(info));
    if (size != sizeof(info)) return false;
    memset(output, 0, sizeof(*output));
    output->thread_id = thread_id;
    output->cpu_time_ns = info.pth_user_time + info.pth_system_time;
    strlcpy(output->name, info.pth_name, sizeof(output->name));
    return true;
}
