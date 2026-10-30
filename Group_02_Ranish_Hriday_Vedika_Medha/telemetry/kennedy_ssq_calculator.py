"""
Kennedy Simulator Sickness Questionnaire (SSQ) Diagnostic Engine
Conforming to Kennedy, Lane, Berbaum & Lilienthal (1993).
Evaluates Cybersickness across Nausea (N), Oculomotor (O), and Disorientation (D).
"""

WEIGHTS = {
    'Nausea': 9.54,
    'Oculomotor': 3.74,
    'Disorientation': 13.92,
    'Total': 3.74
}

def calculate_ssq(symptoms):
    """
    16 standard symptoms rated 0 (None), 1 (Slight), 2 (Moderate), 3 (Severe)
    """
    # Symptom mapping to subscales
    required = ['general_discomfort', 'fatigue', 'headache', 'eyestrain', 'difficulty_focusing',
                'increased_salivation', 'sweating', 'nausea', 'difficulty_concentrating', 'fullness_of_head',
                'blurred_vision', 'dizziness_eyes_open', 'dizziness_eyes_closed', 'vertigo', 'stomach_awareness', 'burping']
    if set(required) - set(symptoms):
        raise ValueError('All 16 Kennedy SSQ symptom responses are required.')
    if any(int(symptoms[key]) < 0 or int(symptoms[key]) > 3 for key in required):
        raise ValueError('Kennedy SSQ responses must be in the 0–3 range.')
    nausea_items = [symptoms['general_discomfort'], symptoms['increased_salivation'], symptoms['sweating'],
                    symptoms['nausea'], symptoms['difficulty_concentrating'], symptoms['fullness_of_head'],
                    symptoms['blurred_vision'], symptoms['dizziness_eyes_open'], symptoms['dizziness_eyes_closed'],
                    symptoms['stomach_awareness'], symptoms['burping']]
    oculomotor_items = [symptoms['general_discomfort'], symptoms['fatigue'], symptoms['headache'],
                        symptoms['eyestrain'], symptoms['difficulty_focusing'], symptoms['difficulty_concentrating'], symptoms['blurred_vision']]
    disorientation_items = [symptoms['difficulty_focusing'], symptoms['nausea'], symptoms['fullness_of_head'],
                            symptoms['dizziness_eyes_open'], symptoms['dizziness_eyes_closed'], symptoms['vertigo'],
                            symptoms['stomach_awareness'], symptoms['burping']]
    
    raw_n = sum(nausea_items)
    raw_o = sum(oculomotor_items)
    raw_d = sum(disorientation_items)
    raw_total = raw_n + raw_o + raw_d
    
    score_n = raw_n * WEIGHTS['Nausea']
    score_o = raw_o * WEIGHTS['Oculomotor']
    score_d = raw_d * WEIGHTS['Disorientation']
    score_ts = raw_total * WEIGHTS['Total']
    
    return {'Nausea': score_n, 'Oculomotor': score_o, 'Disorientation': score_d, 'Total_Severity': score_ts}

def evaluate_ssq_safety(csv_file=None):
    if not csv_file:
        print('No empirical Kennedy SSQ file supplied. No symptom scores or safety conclusion are reported.')
        return None
    import csv
    fields = ['general_discomfort', 'fatigue', 'headache', 'eyestrain', 'difficulty_focusing',
              'increased_salivation', 'sweating', 'nausea', 'difficulty_concentrating', 'fullness_of_head',
              'blurred_vision', 'dizziness_eyes_open', 'dizziness_eyes_closed', 'vertigo', 'stomach_awareness', 'burping']
    with open(csv_file, newline='', encoding='utf-8-sig') as handle:
        reader = csv.DictReader(handle)
        if not reader.fieldnames or 'data_status' not in reader.fieldnames or not set(fields).issubset(reader.fieldnames):
            raise ValueError('SSQ CSV requires data_status and all 16 symptom columns.')
        rows = list(reader)
    if not rows or any(row['data_status'].strip().lower() != 'empirical' for row in rows):
        raise ValueError('No empirical SSQ rows found, or the file includes non-empirical rows.')
    scores = [calculate_ssq({field: int(row[field]) for field in fields}) for row in rows]
    names = ['Nausea', 'Oculomotor', 'Disorientation', 'Total_Severity']
    means = {name: sum(score[name] for score in scores) / len(scores) for name in names}
    print(f'Empirical Kennedy SSQ scores (N={len(scores)}): {means}')
    print('Scores are symptom reports; they do not diagnose sickness or establish a device safety threshold.')
    return means

if __name__ == "__main__":
    evaluate_ssq_safety()
