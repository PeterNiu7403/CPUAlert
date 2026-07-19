import AppKit
import SwiftUI

struct RankedProcessList: View {
    @Bindable var model: MonitorModel

    var body: some View {
        LazyVStack(spacing: 4) {
            if model.selectedResource == .cpu {
                cpuRows
            } else {
                gpuRows
            }
        }
    }

    @ViewBuilder
    private var cpuRows: some View {
        let rows = model.snapshot.processes.prefix(model.showTenRows ? 10 : 5)
        if rows.isEmpty {
            emptyState("Collecting process activity…")
        } else {
            ForEach(rows) { process in
                Button {
                    model.expandedProcess = model.expandedProcess == process.identity
                        ? nil
                        : process.identity
                } label: {
                    HStack(spacing: 8) {
                        processIcon(process)
                        VStack(alignment: .leading, spacing: 1) {
                            Text(process.name)
                                .lineLimit(1)
                            Text("PID \(process.identity.pid)")
                                .font(.caption2)
                                .foregroundStyle(.secondary)
                        }
                        Spacer(minLength: 8)
                        Text(process.cpuUsage, format: .percent.precision(.fractionLength(1)))
                            .monospacedDigit()
                    }
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityHint("Show or hide thread activity")

                if model.expandedProcess == process.identity {
                    ForEach(model.snapshot.expandedThreads) { thread in
                        HStack(spacing: 8) {
                            Image(systemName: "point.3.connected.trianglepath.dotted")
                                .frame(width: 24)
                                .foregroundStyle(.secondary)
                            Text(thread.name ?? "Thread \(thread.id)")
                                .font(.caption)
                                .lineLimit(1)
                            Spacer()
                            Text(thread.cpuUsage, format: .percent.precision(.fractionLength(1)))
                                .font(.caption.monospacedDigit())
                        }
                        .padding(.leading, 12)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private var gpuRows: some View {
        let rows = model.snapshot.gpuGroups.prefix(model.showTenRows ? 10 : 5)
        if rows.isEmpty {
            emptyState("GPU process attribution unavailable")
        } else {
            ForEach(rows) { group in
                HStack(spacing: 8) {
                    Image(systemName: "square.stack.3d.up.fill")
                        .frame(width: 24)
                        .foregroundStyle(.secondary)
                    VStack(alignment: .leading, spacing: 1) {
                        Text(group.name)
                            .lineLimit(1)
                        Text("\(group.members.count) members · GPU activity share")
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                    Spacer(minLength: 8)
                    Text(group.activityShare, format: .percent.precision(.fractionLength(1)))
                        .monospacedDigit()
                }
                .accessibilityLabel(
                    "\(group.name), GPU activity share \(Int((group.activityShare * 100).rounded())) percent"
                )
            }
        }
    }

    private func processIcon(_ process: ProcessMetric) -> some View {
        Group {
            if let icon = NSRunningApplication(
                processIdentifier: process.identity.pid
            )?.icon {
                Image(nsImage: icon)
                    .resizable()
            } else {
                Image(systemName: "app.dashed")
                    .resizable()
                    .foregroundStyle(.secondary)
            }
        }
        .scaledToFit()
        .frame(width: 24, height: 24)
    }

    private func emptyState(_ text: String) -> some View {
        Text(text)
            .font(.caption)
            .foregroundStyle(.secondary)
            .frame(maxWidth: .infinity, minHeight: 72)
    }
}
