﻿using System;
using System.Runtime.Serialization;
using NTM2.Learning;
using NTM2.Memory.Addressing;

namespace NTM2.Controller
{
    [DataContract]
    internal class OutputLayer
    {
        [DataMember]
        private readonly int _outputSize;
        [DataMember]
        private readonly int _controllerSize;
        [DataMember]
        private readonly int _headCount;
        [DataMember]
        private readonly int _memoryUnitSizeM;
        [DataMember]
        private readonly int _headUnitSize;

        //Weights from controller to output
        [DataMember]
        private readonly Unit[][] _hiddenToOutputLayerWeights;

        //Weights from controller to head
        [DataMember]
        private readonly Unit[][][] _hiddenToHeadsWeights;
        
        //Output layer neurons
        [DataMember]
        internal readonly Unit[] OutputLayerNeurons;
        
        //Heads neurons
        [DataMember]
        internal readonly Head[] HeadsNeurons;

        public OutputLayer(int outputSize, int controllerSize, int headCount, int memoryUnitSizeM)
        {
            _outputSize = outputSize;
            _controllerSize = controllerSize;
            _headCount = headCount;
            _memoryUnitSizeM = memoryUnitSizeM;
            _headUnitSize = Head.GetUnitSize(memoryUnitSizeM);
            _hiddenToOutputLayerWeights = UnitFactory.GetTensor2(outputSize, controllerSize + 1);
            _hiddenToHeadsWeights = UnitFactory.GetTensor3(headCount, _headUnitSize, controllerSize + 1);
            HeadsNeurons = new Head[headCount];
        }

        private OutputLayer(Unit[][] hiddenToOutputLayerWeights, Unit[][][] hiddenToHeadsWeights, Unit[] outputLayerNeurons, Head[] headsNeurons, int headCount, int outputSize, int controllerSize, int memoryUnitSizeM, int headUnitSize)
        {
            _hiddenToOutputLayerWeights = hiddenToOutputLayerWeights;
            _hiddenToHeadsWeights = hiddenToHeadsWeights;
            HeadsNeurons = headsNeurons;
            _controllerSize = controllerSize;
            _outputSize = outputSize;
            OutputLayerNeurons = outputLayerNeurons;
            _headCount = headCount;
            _memoryUnitSizeM = memoryUnitSizeM;
            _headUnitSize = headUnitSize;
        }
        
        public void ForwardPropagation(HiddenLayer hiddenLayer)
        {
            //Foreach neuron in classic output layer
            for (int i = 0; i < _outputSize; i++)
            {
                double sum = 0;
                Unit[] weights = _hiddenToOutputLayerWeights[i];

                //Foreach input from hidden layer
                for (int j = 0; j < _controllerSize; j++)
                {
                    sum += weights[j].Value * hiddenLayer.HiddenLayerNeurons[j].Value;
                }

                //Plus threshold
                sum += weights[_controllerSize].Value;
                OutputLayerNeurons[i].Value = Sigmoid.GetValue(sum);
            }

            //Foreach neuron in head output layer
            for (int i = 0; i < _headCount; i++)
            {
                Unit[][] headsWeights = _hiddenToHeadsWeights[i];
                Head head = HeadsNeurons[i];

                for (int j = 0; j < headsWeights.Length; j++)
                {
                    double sum = 0;
                    Unit[] headWeights = headsWeights[j];
                    //Foreach input from hidden layer
                    for (int k = 0; k < _controllerSize; k++)
                    {
                        sum += headWeights[k].Value * hiddenLayer.HiddenLayerNeurons[k].Value;
                    }
                    //Plus threshold
                    sum += headWeights[_controllerSize].Value;
                    head[j].Value += sum;
                }
            }
        }

        public OutputLayer Clone()
        {
            Unit[] outputLayer = UnitFactory.GetVector(_outputSize);
            Head[] heads = Head.GetVector(_headCount, i => _memoryUnitSizeM);

            return new OutputLayer(_hiddenToOutputLayerWeights, _hiddenToHeadsWeights, outputLayer, heads, _headCount, _outputSize, _controllerSize, _memoryUnitSizeM, _headUnitSize);
        }

        public void BackwardErrorPropagation(double[] knownOutput, HiddenLayer hiddenLayer)
        {
            for (int j = 0; j < _outputSize; j++)
            {
                //Delta
                OutputLayerNeurons[j].Gradient = OutputLayerNeurons[j].Value - knownOutput[j];
            }

            //Output error backpropagation
            for (int j = 0; j < _outputSize; j++)
            {
                Unit unit = OutputLayerNeurons[j];
                Unit[] weights = _hiddenToOutputLayerWeights[j];
                for (int i = 0; i < _controllerSize; i++)
                {
                    hiddenLayer.HiddenLayerNeurons[i].Gradient += weights[i].Value * unit.Gradient;
                }
            }

            //Heads error backpropagation
            for (int j = 0; j < _headCount; j++)
            {
                Head head = HeadsNeurons[j];
                Unit[][] weights = _hiddenToHeadsWeights[j];
                for (int k = 0; k < _headUnitSize; k++)
                {
                    Unit unit = head[k];
                    Unit[] weightsK = weights[k];
                    for (int i = 0; i < _controllerSize; i++)
                    {
                        hiddenLayer.HiddenLayerNeurons[i].Gradient += unit.Gradient * weightsK[i].Value;
                    }
                }
            }

            //Wyh1 error backpropagation
            for (int i = 0; i < _outputSize; i++)
            {
                Unit[] wyh1I = _hiddenToOutputLayerWeights[i];
                double yGrad = OutputLayerNeurons[i].Gradient;
                for (int j = 0; j < _controllerSize; j++)
                {
                    wyh1I[j].Gradient += yGrad * hiddenLayer.HiddenLayerNeurons[j].Value;
                }
                wyh1I[_controllerSize].Gradient += yGrad;
            }

            //TODO refactor names
            //Wuh1 error backpropagation
            for (int i = 0; i < _headCount; i++)
            {
                Head head = HeadsNeurons[i];
                Unit[][] units = _hiddenToHeadsWeights[i];
                for (int j = 0; j < _headUnitSize; j++)
                {
                    Unit headUnit = head[j];
                    Unit[] wuh1ij = units[j];

                    for (int k = 0; k < _controllerSize; k++)
                    {
                        Unit unit = hiddenLayer.HiddenLayerNeurons[k];
                        wuh1ij[k].Gradient += headUnit.Gradient * unit.Value;
                    }
                    wuh1ij[_controllerSize].Gradient += headUnit.Gradient;
                }
            }
        }

        public void UpdateWeights(Action<Unit> updateAction)
        {
            Action<Unit[][]> tensor2UpdateAction = Unit.GetTensor2UpdateAction(updateAction);
            Action<Unit[][][]> tensor3UpdateAction = Unit.GetTensor3UpdateAction(updateAction);

            tensor2UpdateAction(_hiddenToOutputLayerWeights);
            tensor3UpdateAction(_hiddenToHeadsWeights);
        }

        public void UpdateWeights(IWeightUpdater weightUpdater)
        {
            weightUpdater.UpdateWeight(_hiddenToOutputLayerWeights);
            weightUpdater.UpdateWeight(_hiddenToHeadsWeights);
        }

        public double[] GetOutput()
        {
            double[] output = new double[OutputLayerNeurons.Length];
            for (int i = 0; i < OutputLayerNeurons.Length; i++)
            {
                output[i] = OutputLayerNeurons[i].Value;
            }
            return output;
        }
    }
}
