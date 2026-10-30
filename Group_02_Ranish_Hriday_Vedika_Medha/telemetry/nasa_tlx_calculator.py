"""
NASA Task Load Index (NASA-TLX) Cognitive Workload Engine
Conforming to Hart & Staveland (1988) Cognitive Ergonomics.
Evaluates 6 subjective subscales: Mental Demand, Physical Demand,
Temporal Demand, Performance, Effort, and Frustration.
"""
import math
import csv

def calculate_tlx(subscales):
    """
    subscales: dict with keys ['mental', 'physical', 'temporal', 'performance', 'effort', 'frustration']
    Each value between 0 and 100.
    """
    keys = ['mental', 'physical', 'temporal', 'performance', 'effort', 'frustration']
    if set(keys) - set(subscales):
        raise ValueError("All six NASA-TLX subscales are required.")
    if any(float(subscales[k]) < 0 or float(subscales[k]) > 100 for k in keys):
        raise ValueError("NASA-TLX subscales must be in the 0–100 range.")
    raw_tlx = sum(float(subscales[k]) for k in keys) / len(keys)
    return raw_tlx

def evaluate_tlx_comparison(csv_file=None):
    if not csv_file:
        print("No empirical NASA-TLX file supplied. No participant scores are reported.")
        return None
    names = ['mental', 'physical', 'temporal', 'performance', 'effort', 'frustration']
    with open(csv_file, newline='', encoding='utf-8-sig') as handle:
        reader = csv.DictReader(handle)
        if not reader.fieldnames or 'data_status' not in reader.fieldnames:
            raise ValueError('NASA-TLX CSV must contain data_status=empirical for every response row.')
        if not set(names).issubset(reader.fieldnames):
            raise ValueError('NASA-TLX CSV requires mental, physical, temporal, performance, effort, and frustration columns.')
        rows = list(reader)
    if not rows or any(row['data_status'].strip().lower() != 'empirical' for row in rows):
        raise ValueError('No empirical NASA-TLX rows found, or the file includes non-empirical rows.')
    values = [calculate_tlx({name: float(row[name]) for name in names}) for row in rows]
    mean = sum(values) / len(values)
    variance = sum((value - mean) ** 2 for value in values) / max(1, len(values) - 1)
    print(f'Empirical NASA-TLX raw mean: {mean:.2f} / 100 (N={len(values)})')
    print(f'Sample SD: {math.sqrt(variance):.2f}')
    return {'n': len(values), 'mean': mean, 'sd': math.sqrt(variance)}

if __name__ == "__main__":
    evaluate_tlx_comparison()
